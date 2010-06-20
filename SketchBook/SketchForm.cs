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
	class SketchForm : Form, IStylusSyncPlugin {
		Book      Book;
		PenStroke CurrentStroke = null;
		PointF ToCanvasCoordinate( PointF screencoord ) { return new PointF( screencoord.X - ClientSize.Width/2, screencoord.Y - ClientSize.Height/2 ); }
		float DpiX, DpiY;

		RealTimeStylus RTS;
		public SketchForm() {
			Book = Book.CreateOrLoad( Path.Combine(Application.UserAppDataPath,"default.book") );
			BackColor       = Color.White;
			DoubleBuffered  = true;
			ForeColor       = Color.Gray;
			FormBorderStyle = FormBorderStyle.None;
			StartPosition   = FormStartPosition.CenterScreen;
			WindowState     = FormWindowState.Maximized;
			using ( var fx = CreateGraphics() ) {
				DpiX = fx.DpiX;
				DpiY = fx.DpiY;
			}
			RTS = new RealTimeStylus(this,true);
			RTS.SyncPluginCollection.Add(this);
		}

		protected override void OnLoad( EventArgs e ) {
			RTS.Enabled = true;
			Invalidate();
			base.OnLoad(e);
		}

		protected override void Dispose( bool disposing ) {
			if ( disposing ) {
				RTS.SyncPluginCollection.Remove(this);
				using ( RTS ) {}
			}
			base.Dispose(disposing);
		}

		protected override void OnPaint( PaintEventArgs e ) {
			var fx = e.Graphics;

			Book.OpenPage.DrawTo( fx, ClientSize.Width, ClientSize.Height );
			fx.TranslateTransform( ClientSize.Width/2f, ClientSize.Height/2f );
			if ( CurrentStroke != null ) CurrentStroke.DrawTo(fx);

#if true
			int y = 10;
			Action<string> writeln = s => {
				TextRenderer.DrawText( fx, s, Font, new Point(10,y), ForeColor, BackColor );
				y += TextRenderer.MeasureText( s, Font ).Height;
			};
			writeln(string.Format("Book    Pages: {0}    Size: {1}", Book.Pages.Count, Pretty.Bytes(Book.SizeInBytes) ));
			if ( Book.OpenPage!=null ) writeln(string.Format("Page    Strokes: {0}", Book.OpenPage._DebugStats_StrokesCount ));
#endif
			fx.ResetTransform();

			if ( !RTS.Enabled )
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

		protected override void OnMouseUp( MouseEventArgs e ) {
			if ( CurrentStroke != null ) CurrentStroke.Points.Add( ToCanvasCoordinate(e.Location) );
			switch ( e.Button ) {
			case MouseButtons.Left:
				break;
			}
			Invalidate();
			base.OnMouseUp(e);
		}

		// http://msdn.microsoft.com/en-us/library/microsoft.stylusinput.stylussyncplugincollection_members(v=VS.90).aspx
		class StylusState {
			public bool[] Buttons = new bool[0];

			public StylusState( Stylus stylus ) {
				Buttons = new bool[stylus.Buttons.Count];
				var names = Enumerable.Range(0,Buttons.Length).Select(i=>stylus.Buttons.GetName(i)).ToList();
				TipIndex = names.IndexOf(names.First(n=>n.Contains("Tip")));
				BarrelIndex = names.IndexOf(names.First(n=>n.Contains("Barrel")));
			}
			public int TipIndex, BarrelIndex;
			public bool Tip { get { return Buttons[TipIndex]; } }
			public bool Barrel { get { return Buttons[BarrelIndex]; } }
		}

		Dictionary<Stylus,StylusState> StylusStates = new Dictionary<Stylus,StylusState>();
		DataInterestMask IStylusSyncPlugin.DataInterest { get { return DataInterestMask.Packets | DataInterestMask.StylusButtonDown | DataInterestMask.StylusButtonUp | DataInterestMask.StylusDown | DataInterestMask.StylusUp; }}
		void IStylusSyncPlugin.CustomStylusDataAdded( RealTimeStylus sender, CustomStylusData data ) {}
		void IStylusSyncPlugin.Error( RealTimeStylus sender, ErrorData data ) {}
		void IStylusSyncPlugin.InAirPackets( RealTimeStylus sender, InAirPacketsData data ) {}
		void IStylusSyncPlugin.Packets( RealTimeStylus sender, PacketsData data ) {
			BeginInvoke(new Action(()=>{
				if ( CurrentStroke != null )
				for ( int i=0 ; i<data.Count ; i += data.PacketPropertyCount )
				{
					var point = new PointF(data[i+0]*DpiX/2540f, data[i+1]*DpiY/2540f);
					if ( point != CurrentStroke.Points.LastOrDefault() ) CurrentStroke.Points.Add(ToCanvasCoordinate(point));
				}
				Invalidate();
			}));
		}
		void IStylusSyncPlugin.RealTimeStylusDisabled( RealTimeStylus sender, RealTimeStylusDisabledData data ) {}
		void IStylusSyncPlugin.RealTimeStylusEnabled( RealTimeStylus sender, RealTimeStylusEnabledData data ) {}
		void IStylusSyncPlugin.StylusButtonDown( RealTimeStylus sender, StylusButtonDownData data ) {
			BeginInvoke(new Action(()=>{
				var stylus = data.Stylus;
				if (!StylusStates.ContainsKey(stylus)) StylusStates.Add(stylus,new StylusState(stylus));
				var state = StylusStates[stylus];

				if ( stylus.Buttons.Count > state.Buttons.Length ) {
					var buttons = new bool[stylus.Buttons.Count];
					Array.Copy(state.Buttons,buttons,state.Buttons.Length);
					state.Buttons = buttons;
				}
				state.Buttons[data.ButtonIndex] = true;
			}));
		}
		void IStylusSyncPlugin.StylusButtonUp( RealTimeStylus sender, StylusButtonUpData data ) {
			BeginInvoke(new Action(()=>{
				var stylus = data.Stylus;
				if (!StylusStates.ContainsKey(stylus)) StylusStates.Add(stylus,new StylusState(stylus));
				var state = StylusStates[stylus];

				if ( stylus.Buttons.Count > state.Buttons.Length ) {
					var buttons = new bool[stylus.Buttons.Count];
					Array.Copy(state.Buttons,buttons,state.Buttons.Length);
					state.Buttons = buttons;
				}
				state.Buttons[data.ButtonIndex] = false;
			}));
		}
		void IStylusSyncPlugin.StylusDown( RealTimeStylus sender, StylusDownData data ) {
			BeginInvoke(new Action(()=>{
				var stylus = data.Stylus;
				if (!StylusStates.ContainsKey(stylus)) StylusStates.Add(stylus,new StylusState(stylus));
				var state = StylusStates[stylus];

				if ( state.Tip && !state.Barrel && CurrentStroke == null ) CurrentStroke = new PenStroke();
				if ( CurrentStroke != null ) for ( int i=0 ; i<data.Count ; i += data.PacketPropertyCount ) {
					var point = new PointF(data[i+0]*DpiX/2540f, data[i+1]*DpiY/2540f);
					if ( point != CurrentStroke.Points.LastOrDefault() ) CurrentStroke.Points.Add(ToCanvasCoordinate(point));
				}
				Invalidate();
			}));
		}
		void IStylusSyncPlugin.StylusInRange( RealTimeStylus sender, StylusInRangeData data ) {}
		void IStylusSyncPlugin.StylusOutOfRange( RealTimeStylus sender, StylusOutOfRangeData data ) {}
		void IStylusSyncPlugin.StylusUp( RealTimeStylus sender, StylusUpData data ) {
			if ( CurrentStroke != null ) BeginInvoke(new Action(()=>{
				for ( int i=0 ; i<data.Count ; i += data.PacketPropertyCount ) {
					var point = new PointF(data[i+0]*DpiX/2540f, data[i+1]*DpiY/2540f);
					if ( point != CurrentStroke.Points.LastOrDefault() ) CurrentStroke.Points.Add(ToCanvasCoordinate(point));
				}
				Book.OpenPage.AddStroke(CurrentStroke);
				CurrentStroke = null;
				Book.SaveToDisk();
				Invalidate();
			}));
		}
		void IStylusSyncPlugin.SystemGesture( RealTimeStylus sender, SystemGestureData data ) {}
		void IStylusSyncPlugin.TabletAdded( RealTimeStylus sender, TabletAddedData data ) {}
		void IStylusSyncPlugin.TabletRemoved( RealTimeStylus sender, TabletRemovedData data ) {}

		[STAThread] static void Main() {
			Application.Run( new SketchForm() );
		}
	}
}
