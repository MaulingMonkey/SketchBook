using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;
using System.Collections.Generic;
using System.Linq;

namespace SketchBook {
	[System.ComponentModel.DesignerCategory("")]
	class SketchForm : Form {
		Book      Book;
		PenStroke CurrentStroke = null;
		PointF ToCanvasCoordinate( PointF screencoord ) { return new PointF( screencoord.X - ClientSize.Width/2, screencoord.Y - ClientSize.Height/2 ); }
		StylusMouseMux SMM;

		public SketchForm() {
			Book = Book.CreateOrLoad( Path.Combine(Application.UserAppDataPath,"default.book") );
			BackColor       = Color.White;
			DoubleBuffered  = true;
			ForeColor       = Color.Gray;
			FormBorderStyle = FormBorderStyle.None;
			StartPosition   = FormStartPosition.CenterScreen;
			WindowState     = FormWindowState.Maximized;
			SMM = new StylusMouseMux(this);
		}

		protected override void OnLoad( EventArgs e ) {
			SMM.Enabled = true;
			Invalidate();
			base.OnLoad(e);
		}

		protected override void Dispose( bool disposing ) {
			if ( disposing ) {
				using ( SMM ) {}
			}
			base.Dispose(disposing);
		}

		protected override void OnPaint( PaintEventArgs e ) {
			var fx = e.Graphics;

			Book.OpenPage.DrawTo( fx, ClientSize.Width, ClientSize.Height );
			fx.TranslateTransform( ClientSize.Width/2f, ClientSize.Height/2f );
			//if ( CurrentStroke != null ) CurrentStroke.DrawTo(fx);
			var stroke = SMM.NextStroke;
			if ( stroke != null ) {
				if ( stroke.Completed ) SMM.RemoveStroke();

				switch ( stroke.MouseButtons ) {
				case MouseButtons.Left:
					var ps = new PenStroke() { Points = stroke.Points.Select(p=>ToCanvasCoordinate(p)).ToList() };
					if ( stroke.Completed ) {
						Book.OpenPage.AddStroke(ps);
						Book.SaveToDisk();
					}
					ps.DrawTo(fx);
					break;
				}
			}

			int y = 10;
			Action<string> writeln = s => {
				TextRenderer.DrawText( fx, s, Font, new Point(10,y), ForeColor, BackColor );
				y += TextRenderer.MeasureText( s, Font ).Height;
			};
			writeln(string.Format("SMM     Mouse: {0}    Stylus: {1}", SMM.Debug_MouseStrokes, SMM.Debug_StylusStrokes));
			writeln(string.Format("Book    Pages: {0}    Size: {1}", Book.Pages.Count, Pretty.Bytes(Book.SizeInBytes) ));
			if ( Book.OpenPage!=null ) writeln(string.Format("Page    Strokes: {0}", Book.OpenPage._DebugStats_StrokesCount ));
			fx.ResetTransform();

			if ( !SMM.Enabled )
			using ( var font = new Font("Arial Black",24f) )
			{
				TextRenderer.DrawText( fx, "Enabling stylus, please wait...", font, ClientRectangle, Color.Black, BackColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter );
			}
			base.OnPaint(e);
		}

		protected override void OnResize( EventArgs e ) {
			Invalidate();
			base.OnResize(e);
		}

		protected override void OnKeyDown( KeyEventArgs e ) {
			switch ( e.KeyData ) {
			case Keys.Control | Keys.Z: Book.OpenPage.Undo(); Book.SaveToDisk(); Invalidate(); break;
			case Keys.Control | Keys.Y: Book.OpenPage.Redo(); Book.SaveToDisk(); Invalidate(); break;
			case Keys.Control | Keys.R: Book.OpenPage.Redo(); Book.SaveToDisk(); Invalidate(); break;
			case Keys.Left:  Book.PreviousPage(); Invalidate(); break;
			case Keys.Right: Book.NextPage();     Invalidate(); break;
			}
			base.OnKeyDown( e );
		}

		protected override void OnMouseDown( MouseEventArgs e ) {
			switch ( e.Button ) {
			case MouseButtons.Right:
				if ( e.X < 20 ) {
					Book.PreviousPage();
					Invalidate();
				} else if ( e.X > ClientSize.Width-20 ) {
					if ( e.Y < 20 ) {
						Close();
					} else {
						Book.NextPage();
						Invalidate();
					}
				}
				break;
			}
			Invalidate();
			base.OnMouseDown(e);
		}

		[STAThread] static void Main() {
			Application.Run( new SketchForm() );
		}
	}
}
