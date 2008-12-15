
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.VisualModes;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	internal class BaseVisualThing : VisualThing, IVisualEventReceiver
	{
		#region ================== Constants
		
		#endregion
		
		#region ================== Variables
		
		private ThingTypeInfo info;
		private bool isloaded;
		private ImageData sprite;
		private float cageradius2;
		private Vector2D pos2d;
		private Vector3D boxp1;
		private Vector3D boxp2;
		
		#endregion
		
		#region ================== Properties
		
		#endregion
		
		#region ================== Constructor / Setup
		
		// Constructor
		public BaseVisualThing(Thing t) : base(t)
		{
			// Find thing information
			info = General.Map.Config.GetThingInfo(Thing.Type);
			
			// Find sprite texture
			if(info.Sprite.Length > 0)
			{
				sprite = General.Map.Data.GetSpriteImage(info.Sprite);
				if(sprite != null) sprite.AddReference();
			}
			
			// We have no destructor
			GC.SuppressFinalize(this);
		}
		
		// This builds the thing geometry. Returns false when nothing was created.
		public virtual bool Setup()
		{
			PixelColor sectorcolor = new PixelColor(255, 255, 255, 255);
			
			if(sprite != null)
			{
				// Find the sector in which the thing resides
				Thing.DetermineSector();
				if(Thing.Sector != null)
				{
					// Use sector brightness for color shading
					sectorcolor = new PixelColor(255, unchecked((byte)Thing.Sector.Brightness),
											 unchecked((byte)Thing.Sector.Brightness),
											 unchecked((byte)Thing.Sector.Brightness));
				}
				
				// Check if the texture is loaded
				isloaded = sprite.IsImageLoaded;
				if(isloaded)
				{
					base.Texture = sprite;
					
					// Determine sprite size
					float radius = sprite.ScaledWidth * 0.5f;
					float height = sprite.ScaledHeight;
					
					// Make vertices
					WorldVertex[] verts = new WorldVertex[6];
					verts[0] = new WorldVertex(-radius, 0.0f, 0.0f, sectorcolor.ToInt(), 0.0f, 1.0f);
					verts[1] = new WorldVertex(-radius, 0.0f, height, sectorcolor.ToInt(), 0.0f, 0.0f);
					verts[2] = new WorldVertex(+radius, 0.0f, height, sectorcolor.ToInt(), 1.0f, 0.0f);
					verts[3] = verts[0];
					verts[4] = verts[2];
					verts[5] = new WorldVertex(+radius, 0.0f, 0.0f, sectorcolor.ToInt(), 1.0f, 1.0f);
					SetVertices(verts);
				}
				else
				{
					base.Texture = General.Map.Data.Hourglass3D;
					
					// Determine sprite size
					float radius = info.Width;
					float height = info.Height;
					
					// Make vertices
					WorldVertex[] verts = new WorldVertex[6];
					verts[0] = new WorldVertex(-radius, 0.0f, 0.0f, sectorcolor.ToInt(), 0.0f, 1.0f);
					verts[1] = new WorldVertex(-radius, 0.0f, height, sectorcolor.ToInt(), 0.0f, 0.0f);
					verts[2] = new WorldVertex(+radius, 0.0f, height, sectorcolor.ToInt(), 1.0f, 0.0f);
					verts[3] = verts[0];
					verts[4] = verts[2];
					verts[5] = new WorldVertex(+radius, 0.0f, 0.0f, sectorcolor.ToInt(), 1.0f, 1.0f);
					SetVertices(verts);
				}
			}
			
			// Setup position and size
			Vector3D pos = Thing.Position;
			if(Thing.Sector != null) pos.z += Thing.Sector.FloorHeight;
			SetPosition(pos);
			SetCageSize(info.Width, info.Height);
			SetCageColor(Thing.Color);
			
			// Keep info for object picking
			cageradius2 = info.Width * Angle2D.SQRT2;
			cageradius2 = cageradius2 * cageradius2;
			pos2d = pos;
			boxp1 = new Vector3D(pos.x - info.Width, pos.y - info.Width, pos.z);
			boxp2 = new Vector3D(pos.x + info.Width, pos.y + info.Width, pos.z + info.Height);
			
			// Done
			return true;
		}
		
		// Disposing
		public override void Dispose()
		{
			if(!IsDisposed)
			{
				if(sprite != null)
				{
					sprite.RemoveReference();
					sprite = null;
				}
			}
			
			base.Dispose();
		}
		
		#endregion
		
		#region ================== Methods
		
		// This updates the thing when needed
		public override void Update()
		{
			if(!isloaded)
			{
				// Rebuild sprite geometry when sprite is loaded
				if(sprite.IsImageLoaded)
				{
					Setup();
				}
			}
			
			// Let the base update
			base.Update();
		}

		// This performs a fast test in object picking
		public override bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir)
		{
			float distance2 = Line2D.GetDistanceToLineSq(from, to, pos2d, false);
			return (distance2 <= cageradius2);
		}

		// This performs an accurate test for object picking
		public override bool PickAccurate(Vector3D from, Vector3D to, Vector3D dir, ref float u_ray)
		{
			// TEST
			//u_ray = Line2D.GetNearestOnLine(from, to, pos2d);
			//return true;
			
			Vector3D delta = to - from;
			float tfar = float.MaxValue;
			float tnear = float.MinValue;
			
			// Ray-Box intersection code
			// See http://www.masm32.com/board/index.php?PHPSESSID=eee672d82a12b8b8f1871268f652be82&topic=9941.0
			
			// Check X slab
			if(delta.x == 0.0f)
			{
				if(from.x > boxp2.x || from.x < boxp1.x)
				{
					// Ray is parallel to the planes & outside slab
					return false;
				}
			}
			else
			{
				float tmp = 1.0f / delta.x;
				float t1 = (boxp1.x - from.x) * tmp;
				float t2 = (boxp2.x - from.x) * tmp;
				if(t1 > t2) General.Swap<float>(ref t1, ref t2);
				if(t1 > tnear) tnear = t1;
				if(t2 < tfar) tfar = t2;
				if(tnear > tfar || tfar < 0.0f)
				{
					// Ray missed box or box is behind ray
					return false;
				}
			}
			
			// Check Y slab
			if(delta.y == 0.0f)
			{
				if(from.y > boxp2.y || from.y < boxp1.y)
				{
					// Ray is parallel to the planes & outside slab
					return false;
				}
			}
			else
			{
				float tmp = 1.0f / delta.y;
				float t1 = (boxp1.y - from.y) * tmp;
				float t2 = (boxp2.y - from.y) * tmp;
				if(t1 > t2) General.Swap<float>(ref t1, ref t2);
				if(t1 > tnear) tnear = t1;
				if(t2 < tfar) tfar = t2;
				if(tnear > tfar || tfar < 0.0f)
				{
					// Ray missed box or box is behind ray
					return false;
				}
			}
			
			// Check Z slab
			if(delta.z == 0.0f)
			{
				if(from.z > boxp2.z || from.z < boxp1.z)
				{
					// Ray is parallel to the planes & outside slab
					return false;
				}
			}
			else
			{
				float tmp = 1.0f / delta.z;
				float t1 = (boxp1.z - from.z) * tmp;
				float t2 = (boxp2.z - from.z) * tmp;
				if(t1 > t2) General.Swap<float>(ref t1, ref t2);
				if(t1 > tnear) tnear = t1;
				if(t2 < tfar) tfar = t2;
				if(tnear > tfar || tfar < 0.0f)
				{
					// Ray missed box or box is behind ray
					return false;
				}
			}
			
			// Set interpolation point
			u_ray = (tnear > 0.0f) ? tnear : tfar;
			return true;
		}
		
		#endregion

		#region ================== Events

		// Unused
		public virtual void OnSelectBegin() { }
		public virtual void OnSelectEnd() { }
		public virtual void OnEditBegin() { }
		public virtual void OnEditEnd() { }
		
		#endregion
	}
}