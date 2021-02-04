using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;

namespace ACORN_shells
{
    /// <summary>
    /// Fits a bounding box to a shell segment, minimizing volume.
    /// </summary>
    public class FitModules : GH_Component
    {
        public FitModules()
          : base("Fit Pinbed Modules", "A:FitMods",
              "Fits pinbed module rectangles to a segment bounding box.",
              "ACORN", "Pinbed")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBoxParameter("Box", "B", "Shell segment bounding box.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Width", "W", "Module width [m]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Depth", "D", "Module depth [m]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance Level", "T", "Tolerance = 1e-(tolLevel)", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddRectangleParameter("Module rectangles", "M", "Set of pinbed module rectangles.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Box box = Box.Unset;
            double moduleWidth = 1;
            double moduleDepth = 1;
            double tolLevel = 3;

            if (!DA.GetData(0, ref box)) return;
            if (!DA.GetData(1, ref moduleWidth)) return;
            if (!DA.GetData(2, ref moduleDepth)) return;
            if (!DA.GetData(3, ref tolLevel)) return;

            // calculate number of modules in both width and depth
            //double tol = DocumentTolerance(); // in case segment size is exact multiple of module size (e.g. keystone)
            double tol = Math.Exp(-tolLevel); // in case segment size is exact multiple of module size (e.g. keystone)

            double boxWidth = box.X.Max - box.X.Min - tol;
            double boxDepth = box.Y.Max - box.Y.Min - tol;
            int modulesInWidth = (int)Math.Ceiling(boxWidth / moduleWidth);
            int modulesInDepth = (int)Math.Ceiling(boxDepth / moduleDepth);

            // find bottom plane
            Plane bottomPlane = new Plane(box.Plane);
            bottomPlane.Origin = box.PointAt(0.5, 0.5, 0);
            // move bottom plane origin in order to match rects union center with initial bottomplane origin
            bottomPlane.Origin = bottomPlane.PointAt(- modulesInWidth * moduleWidth / 2, - modulesInDepth * moduleDepth / 2);

            // draw rectangles
            List<Rectangle3d> rects = new List<Rectangle3d>();
            for (int w = 0; w < modulesInWidth; w++)
            {
                for (int d = 0; d < modulesInDepth; d++) 
                {
                    Interval domainWidth = new Interval(w * moduleWidth, (w + 1) * moduleWidth);
                    Interval domainDepth = new Interval(d * moduleDepth, (d + 1) * moduleDepth);
                    Rectangle3d module = new Rectangle3d(bottomPlane, domainWidth, domainDepth);
                    rects.Add(module);
                }                    
            }

            DA.SetDataList(0, rects);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.ACORN_24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("155b50fe-2699-4581-97dd-ff06b28a93b1"); }
        }
    }
}