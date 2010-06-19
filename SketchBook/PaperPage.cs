using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SketchBook {
	[System.ComponentModel.DesignerCategory("")]
	class PaperForm : Form {
		Book      Book = Book.CreateOrLoad( Path.Combine(Application.UserAppDataPath,"default.book") );
		PenStroke CurrentStroke = null;
		PointF ToCanvasCoordinate( PointF screencoord ) { return new PointF( screencoord.X - ClientSize.Width/2, screencoord.Y - ClientSize.Height/2 ); }

		public PaperForm() {
			BackColor       = Color.White;
			DoubleBuffered  = true;
			FormBorderStyle = FormBorderStyle.None;
			StartPosition   = FormStartPosition.CenterScreen;
			WindowState     = FormWindowState.Maximized;
		}

		protected override void OnPaint( PaintEventArgs e ) {
			var fx = e.Graphics;

			Book.OpenPage.DrawTo( fx, ClientSize.Width, ClientSize.Height );
			fx.TranslateTransform( ClientSize.Width/2f, ClientSize.Height/2f );
			if ( CurrentStroke != null ) CurrentStroke.DrawTo(fx);
			base.OnPaint(e);
		}

		protected override void OnResize( EventArgs e ) {
			Invalidate();
			base.OnResize(e);
		}

		protected override void OnMouseDown( MouseEventArgs e ) {
			switch ( e.Button ) {
			case MouseButtons.Left:
				CurrentStroke = new PenStroke() { Points = { ToCanvasCoordinate(e.Location) } };
				break;
			}
			Invalidate();
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove( MouseEventArgs e ) {
			if ( CurrentStroke != null ) CurrentStroke.Points.Add( ToCanvasCoordinate(e.Location) );
			Invalidate();
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp( MouseEventArgs e ) {
			if ( CurrentStroke != null ) CurrentStroke.Points.Add( ToCanvasCoordinate(e.Location) );
			switch ( e.Button ) {
			case MouseButtons.Left:
				Book.OpenPage.AddStroke(CurrentStroke);
				CurrentStroke = null;
				Book.SaveToDisk();
				break;
			}
			Invalidate();
			base.OnMouseUp(e);
		}

		[STAThread] static void Main() {
			Application.Run( new PaperForm() );
		}
	}
}
