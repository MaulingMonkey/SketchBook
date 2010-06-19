using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.Serialization.Formatters.Binary;

namespace SketchBook {
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
	}
}
