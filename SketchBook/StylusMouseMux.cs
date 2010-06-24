using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;
using System.Media;

namespace SketchBook {
	class StylusMouseMux : IStylusSyncPlugin, IStylusAsyncPlugin, IDisposable {
		float FormDpiX, FormDpiY;
		RealTimeStylus RTS;
		Form Form;

		public StylusMouseMux( Form form ) {
			using ( var fx = form.CreateGraphics() ) {
				FormDpiX = fx.DpiX;
				FormDpiY = fx.DpiY;
			}
			form.MouseDown += OnMouseDown;
			form.MouseMove += OnMouseMove;
			form.MouseUp   += OnMouseUp;
			RTS = new RealTimeStylus(form,true);
			RTS.AsyncPluginCollection.Add(this);
			Form = form;
		}

		public bool Enabled { get { return RTS.Enabled; } set { RTS.Enabled = value; } }

		public void Dispose() {
			using ( RTS ) {}
		}

		public class Stroke {
			public List<PointF> Points = new List<PointF>();
			public MouseButtons MouseButtons;
			public bool Completed;
		}

		readonly Queue<Stroke> MouseStrokes  = new Queue<Stroke>();
		readonly Queue<Stroke> StylusStrokes = new Queue<Stroke>();
		public int Debug_MouseStrokes  { get { return MouseStrokes .Count; }}
		public int Debug_StylusStrokes { get { return StylusStrokes.Count; }}

		float Dist2( PointF a, PointF b ) {
			var dx = a.X-b.X;
			var dy = a.Y-b.Y;
			return dx*dx+dy*dy;
		}

		public Stroke NextStroke { get {
			lock ( StylusStrokes ) {
				var mouse_stroke  = MouseStrokes .Count>0 ? MouseStrokes .Peek() : null;
				var stylus_stroke = StylusStrokes.Count>0 ? StylusStrokes.Peek() : null;

				while
					(  mouse_stroke  != null && mouse_stroke.Points.Count>0
					&& stylus_stroke != null && stylus_stroke.Points.Count>0
					&& Dist2(mouse_stroke.Points.First(),stylus_stroke.Points.First()) > 2*2
					)
				{
					// We have a mouse and stylus stroke, but they don't match up!
					// Reject from the bigger queue and hope that's right.
					// Assume stylus events are delayed -- if queues are event, reject from the stylus queue.
					// Known causes include:
					//   Clicking on a form to focus it causes mouse events but not stylus events
					if ( MouseStrokes.Count > StylusStrokes.Count ) {
						MouseStrokes.Dequeue();
						mouse_stroke = MouseStrokes.Count>0 ? MouseStrokes.Peek() : null;
					} else {
						StylusStrokes.Dequeue();
						stylus_stroke = StylusStrokes.Count>0 ? StylusStrokes.Peek() : null;
					}
					SystemSounds.Beep.Play();
				}

				if ( mouse_stroke != null ) {
					if ( stylus_stroke != null && stylus_stroke.Points.Count>0 ) {
						// Debug correlation...
						var mouse_start  = mouse_stroke.Points.First();
						var stylus_start = stylus_stroke.Points.First();
						var dist2 = Dist2(mouse_stroke.Points.First(),stylus_stroke.Points.First());
						Debug.Assert( dist2 < 1*1, "Mouse and Stylus started more than 1 apart" );

						return new Stroke() { MouseButtons = mouse_stroke.MouseButtons, Points = new List<PointF>(stylus_stroke.Points), Completed = mouse_stroke.Completed && stylus_stroke.Completed };
					} else {
						// Only mouse data so far...
						return stylus_stroke;
					}
				} else if ( StylusStrokes.Count > 0 ) {
					// Only stylus data so far...
					return new Stroke() { MouseButtons = stylus_stroke.MouseButtons, Points = new List<PointF>(stylus_stroke.Points) };
				} else {
					// No data from mouse or stylus
					return null;
				}
			}
		}}

		public void RemoveStroke() {
			MouseStrokes.Dequeue();
			StylusStrokes.Dequeue();
		}



		Stroke PendingMouseStroke;
		MouseButtons HeldMouseButtons;
		void OnMouseDown( object sender, MouseEventArgs data ) {
			if ( PendingMouseStroke == null ) {
				PendingMouseStroke = new Stroke() { MouseButtons = data.Button, Points = { data.Location } };
				MouseStrokes.Enqueue(PendingMouseStroke);
			} else {
				PendingMouseStroke.Points.Add( data.Location );
				HeldMouseButtons |= data.Button;
			}
			Form.Invalidate();
		}
		void OnMouseUp  ( object sender, MouseEventArgs data ) {
			Debug.Assert( PendingMouseStroke != null );
			PendingMouseStroke.Points.Add( data.Location );
			HeldMouseButtons &=~ data.Button;
			if ( HeldMouseButtons == MouseButtons.None ) {
				PendingMouseStroke.Completed = true;
				PendingMouseStroke = null;
			}
			Form.Invalidate();
		}
		void OnMouseMove( object sender, MouseEventArgs data ) {
			if ( PendingMouseStroke != null ) {
				PendingMouseStroke.Points.Add(data.Location);
				Form.Invalidate();
			}
		}



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
		Stroke PendingStylusStroke;
		readonly Dictionary<Stylus,StylusState> StylusStates = new Dictionary<Stylus,StylusState>();
		public DataInterestMask DataInterest { get { return DataInterestMask.Packets | DataInterestMask.StylusButtonDown | DataInterestMask.StylusButtonUp | DataInterestMask.StylusDown | DataInterestMask.StylusUp; }}
		public void CustomStylusDataAdded( RealTimeStylus sender, CustomStylusData data ) {}
		public void Error( RealTimeStylus sender, ErrorData data ) {}
		public void InAirPackets( RealTimeStylus sender, InAirPacketsData data ) {}
		public void Packets( RealTimeStylus sender, PacketsData data ) {
			lock ( StylusStrokes ) {
				Debug.Assert( PendingStylusStroke != null );
				for ( int i=0 ; i<data.Count ; i += data.PacketPropertyCount ) {
					var point = new PointF(data[i+0]*FormDpiX/2540f, data[i+1]*FormDpiY/2540f);
					if ( point != PendingStylusStroke.Points.LastOrDefault() ) PendingStylusStroke.Points.Add(point);
				}
			}
			Form.Invalidate();
		}
		public void RealTimeStylusDisabled( RealTimeStylus sender, RealTimeStylusDisabledData data ) {}
		public void RealTimeStylusEnabled( RealTimeStylus sender, RealTimeStylusEnabledData data ) {}
		public void StylusButtonDown( RealTimeStylus sender, StylusButtonDownData data ) {
			if (!StylusStates.ContainsKey(data.Stylus)) StylusStates.Add( data.Stylus, new StylusState(data.Stylus) );
			var state = StylusStates[data.Stylus];
			state.Buttons[data.ButtonIndex] = true;
		}
		public void StylusButtonUp( RealTimeStylus sender, StylusButtonUpData data ) {
			if (!StylusStates.ContainsKey(data.Stylus)) StylusStates.Add( data.Stylus, new StylusState(data.Stylus) );
			var state = StylusStates[data.Stylus];
			state.Buttons[data.ButtonIndex] = false;
		}
		public void StylusDown( RealTimeStylus sender, StylusDownData data ) {
			if (!StylusStates.ContainsKey(data.Stylus)) StylusStates.Add( data.Stylus, new StylusState(data.Stylus) );
			var state = StylusStates[data.Stylus];
			Debug.Assert( PendingStylusStroke == null );
			PendingStylusStroke = new Stroke() { MouseButtons = state.Barrel ? MouseButtons.Right : MouseButtons.Left };
			for ( int i=0 ; i<data.Count ; i += data.PacketPropertyCount ) {
				var point = new PointF(data[i+0]*FormDpiX/2540f, data[i+1]*FormDpiY/2540f);
				if ( point != PendingStylusStroke.Points.LastOrDefault() ) PendingStylusStroke.Points.Add(point);
			}
			lock ( StylusStrokes ) StylusStrokes.Enqueue(PendingStylusStroke);
			Form.Invalidate();
		}
		public void StylusInRange( RealTimeStylus sender, StylusInRangeData data ) {}
		public void StylusOutOfRange( RealTimeStylus sender, StylusOutOfRangeData data ) {}
		public void StylusUp( RealTimeStylus sender, StylusUpData data ) {
			Debug.Assert( PendingStylusStroke != null );
			lock ( StylusStrokes ) {
				for ( int i=0 ; i<data.Count ; i += data.PacketPropertyCount ) {
					var point = new PointF(data[i+0]*FormDpiX/2540f, data[i+1]*FormDpiY/2540f);
					if ( point != PendingStylusStroke.Points.LastOrDefault() ) PendingStylusStroke.Points.Add(point);
				}
				PendingStylusStroke.Completed = true;
				PendingStylusStroke = null;
			}
			Form.Invalidate();
		}
		public void SystemGesture( RealTimeStylus sender, SystemGestureData data ) {}
		public void TabletAdded( RealTimeStylus sender, TabletAddedData data ) {}
		public void TabletRemoved( RealTimeStylus sender, TabletRemovedData data ) {}
	}
}
