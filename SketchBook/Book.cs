using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Threading;
using System.Runtime.Serialization;

namespace SketchBook {
	[Serializable] class Book {
		public List<Page> Pages = new List<Page>() { new Page() };
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
			if ( File.Exists( path ) )
			using ( var stream = File.Open(path,FileMode.Open,FileAccess.Read) )
			{
				var bf = new BinaryFormatter();
				var book = (Book)bf.Deserialize(stream);
				book.Path = path;
				book.SizeInBytes = stream.Position;
				return book;
			} else {
				return new Book() { Path=path };
			}
		}

		[NonSerialized] public long SizeInBytes = 0;
		public void SaveToDisk() {
			using ( var stream = File.Open(Path,FileMode.OpenOrCreate,FileAccess.Write) ) {
				var bf = new BinaryFormatter();
				bf.Serialize( stream, this );
				SizeInBytes = stream.Position;
			}
		}

		[OnDeserialized] void FixupBackgroundLock( StreamingContext sc ) { Background = new object(); }

		[NonSerialized] object Background = new object();
		[NonSerialized] Book SerializeNext;
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
						}
					}
				});
				SerializeNext = clone;
			}
		}
	}
}
