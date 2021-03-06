﻿using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace SketchBook {
	[Serializable] class Book : IDisposable {
		public List<Page> Pages = new List<Page>() { new Page() };

		public void Dispose() {
			foreach ( var page in Pages ) page.Dispose();
		}

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

		public void NextPage() {
			if ( ++OpenPageIndex >= Pages.Count ) {
				Pages.Add(new Page());
			}
		}
		public void PreviousPage() {
			if ( OpenPageIndex <= 0 ) {
				SystemSounds.Beep.Play();
			} else {
				--OpenPageIndex;
			}
		}

		private Book() {}

		[NonSerialized] string Path;
		public static Book CreateOrLoad( string path ) {
			bool anyexisted = false;

			foreach ( var ext in new[] { ".new", "" } )
			if ( File.Exists( path+ext ) )
			using ( var stream = File.Open(path+ext,FileMode.Open,FileAccess.Read) )
			try
			{
				anyexisted = true;
				var bf = new BinaryFormatter();
				var book = (Book)bf.Deserialize(stream,null);
				book.Path = path;
				book.SizeInBytes = stream.Position;
				return book;
			} catch ( Exception ) {}

			if (anyexisted) throw new Exception( "Error loading existing files" );
			return new Book() { Path=path };
		}

		[NonSerialized] public long SizeInBytes = 0;
		void SaveToDisk() {
			if ( File.Exists(Path+".new") && !File.Exists(Path) ) File.Move(Path+".new",Path);

			using ( var stream = File.Open(Path+".new",FileMode.OpenOrCreate,FileAccess.Write) ) {
				var bf = new BinaryFormatter();
				bf.Serialize( stream, this );
				SizeInBytes = stream.Position;
			}

			using ( var stream = File.Open(Path+".new",FileMode.Open,FileAccess.Read) ) {
				var bf = new BinaryFormatter();
				var book = (Book)bf.Deserialize(stream); // verify savefile
			}

			File.Delete(Path);
			File.Move(Path+".new",Path);

			for ( int i=0 ; i<Pages.Count ; ++i ) {
				var page = Pages[i];
				if ( page._SerializationHack_Cache != null ) {
					page._SerializationHack_Cache.Save(string.Format("{0}.{1}.png",Path,i+1),ImageFormat.Png);
				}
			}
		}

		[OnDeserialized] void FixupAfterDeserialize( StreamingContext sc ) {
			Background = new object();
			ToDispose = new List<IDisposable>();
		}

		[NonSerialized] object Background = new object();
		[NonSerialized] Book SerializeNext;
		[NonSerialized] List<IDisposable> ToDispose = new List<IDisposable>();
		public void BackgroundSaveToDisk() {
			var clone = new Book()
				{ Pages = Pages.Select(page=>page.Clone()).ToList()
				, OpenPageIndex = OpenPageIndex
				, Path = Path
				, SizeInBytes = SizeInBytes
				};
			lock ( Background ) {
				bool spawn_bg_worker = SerializeNext==null;
				if ( spawn_bg_worker ) ThreadPool.QueueUserWorkItem(o=>{
					Book book = null;
					lock ( Background ) book = SerializeNext;
					while ( book != null ) {
						book.SaveToDisk();
						lock ( Background ) {
							SizeInBytes = book.SizeInBytes;
							SerializeNext = book = (book==SerializeNext) ? null : SerializeNext;

							if ( book==null ) {
								foreach ( var d in ToDispose ) using ( d ) {}
								ToDispose.Clear();
							}
						}
					}
				});
				SerializeNext = clone;
				ToDispose.Add(clone);
			}
		}
	}
}
