using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SketchBook {
	static class Pretty {
		public static string Bytes( long count ) {
			if ( count <=          10000 ) return count/1+" B";
			if ( count <=       10000000 ) return count/1000+" KB";
			if ( count <=    10000000000 ) return count/1000000+" MB";
			if ( count <= 10000000000000 ) return count/1000000000+" GB";
			return count/1000000000000+" TB";
		}
	}
}
