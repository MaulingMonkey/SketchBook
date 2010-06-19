using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace SketchBook {
	[Serializable] class PenStroke {
		public int                   PenColor = unchecked((int)0xFF000000);
		public float                 PenWidth = 1f;
		public Pen CreatePen() { return new Pen(Color.FromArgb(PenColor)) { Width = PenWidth }; }
		public SmoothingMode         SmoothingMode = SmoothingMode.AntiAlias;
		public readonly List<PointF> Points        = new List<PointF>(); // relative to center of page
		public void DrawTo( Graphics fx ) {
			if ( Points.Count >= 2 ) {
				var old_sm = fx.SmoothingMode;
				fx.SmoothingMode = SmoothingMode;
				using ( var pen = CreatePen() ) fx.DrawLines( pen, Points.ToArray() );
				fx.SmoothingMode = old_sm;
			}
		}
	}

	[Serializable] class Page {
		readonly List<PenStroke> Strokes = new List<PenStroke>();
		[OptionalField] List<PenStroke> RedoHistory = new List<PenStroke>();
		[NonSerialized] Bitmap Cache;

		[OnDeserialized] void FixupAfterDeserialized( StreamingContext sc ) {
			if ( RedoHistory == null ) RedoHistory = new List<PenStroke>();
		}

		public void AddStroke( PenStroke stroke ) {
			RedoHistory.Clear();
			DoAddStroke(stroke);
		}

		void DoAddStroke( PenStroke stroke ) {
			Strokes.Add( stroke );
			if ( Cache != null ) using ( var fx = Graphics.FromImage(Cache) ) {
				fx.TranslateTransform( Cache.Width/2f, Cache.Height/2f );
				stroke.DrawTo(fx);
			}
		}

		private void RedrawCache() {
			if ( Cache != null)
			using ( var fx = Graphics.FromImage(Cache) )
			{
				fx.Clear( Color.White );
				fx.TranslateTransform( Cache.Width/2f, Cache.Height/2f );
				foreach ( var stroke in Strokes ) stroke.DrawTo(fx);
			}
		}

		public void DrawTo( Graphics rfx, int w, int h ) {
			if (w%2==1) ++w;
			if (h%2==1) ++h;

			if ( Cache == null ) {
				Cache = new Bitmap(w,h,PixelFormat.Format32bppArgb);
				RedrawCache();
			} else if ( Cache.Width < w || Cache.Height < h ) {
				var oldw = Cache.Width;
				var oldh = Cache.Height;
				Cache.Dispose();
				Cache = new Bitmap(Math.Max(oldw,w),Math.Max(oldh,h),PixelFormat.Format32bppArgb);
				RedrawCache();
			}
			rfx.DrawImage( Cache, (w-Cache.Width)/2, (h-Cache.Height)/2, Cache.Width, Cache.Height );
		}

		public void Undo() {
			if ( Strokes.Count <= 0 ) {
				SystemSounds.Beep.Play();
				return;
			}
			RedoHistory.Add( Strokes[Strokes.Count-1] );
			Strokes.RemoveAt(Strokes.Count-1);
			//RedrawCache();
			using ( Cache ) {}
			Cache = null;
		}

		public void Redo() {
			if ( RedoHistory.Count <= 0 ) {
				SystemSounds.Beep.Play();
				return;
			}
			DoAddStroke(RedoHistory[RedoHistory.Count-1]);
			RedoHistory.RemoveAt(RedoHistory.Count-1);
		}
	}

	[Serializable] class Book {
		public readonly List<Page> Pages = new List<Page>() { new Page() };
		int OpenPageIndex = 0;
		public Page OpenPage { get {
			return OpenPageIndex==-1 ? null : Pages[OpenPageIndex];
		} set {
			if ( value == null ) {
				OpenPageIndex = -1;
			} else {
				var index = Pages.IndexOf(value);
				if ( index == -1 ) throw new ArgumentException( "Page doesn't belong to this book" );
				OpenPageIndex = index;
			}
		}}

		private Book() {}

		[NonSerialized] string Path;
		public static Book CreateOrLoad( string path ) {
			if ( File.Exists( path ) )
			using ( var stream = File.Open(path,FileMode.Open,FileAccess.Read) )
			{
				var bf = new BinaryFormatter();
				var book = (Book)bf.Deserialize(stream);
				book.Path = path;
				return book;
			} else {
				return new Book() { Path=path };
			}
		}

		public void SaveToDisk() {
			using ( var stream = File.Open(Path,FileMode.OpenOrCreate,FileAccess.Write) ) {
				var bf = new BinaryFormatter();
				bf.Serialize( stream, this );
			}
		}
	}
}
