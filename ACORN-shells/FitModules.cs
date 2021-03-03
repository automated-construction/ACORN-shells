using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using System.Drawing;

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
            pManager.AddBoxParameter("Boxes", "B", "Shell segment bounding boxes (use flattened list).", GH_ParamAccess.list);
            // move to tree and flatten at beginning
            pManager.AddNumberParameter("Width", "W", "Module width [m]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Depth", "D", "Module depth [m]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance Level", "T", "Tolerance = 1e-(tolLevel)", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddRectangleParameter("Module rectangles", "MT", "Set of pinbed module rectangles (as DataTree).", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Box box = Box.Unset;
            List<Box> boxes = new List<Box>();
            double moduleWidth = 1;
            double moduleDepth = 1;
            double tolLevel = 3;

            //if (!DA.GetData(0, box)) return;
            if (!DA.GetDataList(0, boxes)) return;
            if (!DA.GetData(1, ref moduleWidth)) return;
            if (!DA.GetData(2, ref moduleDepth)) return;
            if (!DA.GetData(3, ref tolLevel)) return;

            // calculate number of modules in both width and depth
            //double tol = DocumentTolerance(); // in case segment size is exact multiple of module size (e.g. keystone)
            double tol = Math.Exp(-tolLevel); // in case segment size is exact multiple of module size (e.g. keystone)

            DataTree<Rectangle3d> moduleTree = new DataTree<Rectangle3d>();
            // moduleTree logic: {b,w,d} == {bounding boxes (of segments); modules in width, modules in depth}

            for (int b = 0; b < boxes.Count; b++)
            {
                Box box = boxes[b];

                double boxWidth = box.X.Max - box.X.Min - tol;
                double boxDepth = box.Y.Max - box.Y.Min - tol;
                int modulesInWidth = (int)Math.Ceiling(boxWidth / moduleWidth);
                int modulesInDepth = (int)Math.Ceiling(boxDepth / moduleDepth);

                // find bottom plane
                Plane bottomPlane = new Plane(box.Plane);
                bottomPlane.Origin = box.PointAt(0.5, 0.5, 0);
                // move bottom plane origin in order to match rects union center with initial bottomplane origin
                bottomPlane.Origin = bottomPlane.PointAt(-modulesInWidth * moduleWidth / 2, -modulesInDepth * moduleDepth / 2);

                for (int w = 0; w < modulesInWidth; w++)
                {                  
                    List<Rectangle3d> modules = new List<Rectangle3d>();

                    for (int d = 0; d < modulesInDepth; d++)
                    {
                        Interval domainWidth = new Interval(w * moduleWidth, (w + 1) * moduleWidth);
                        Interval domainDepth = new Interval(d * moduleDepth, (d + 1) * moduleDepth);
                        Rectangle3d module = new Rectangle3d(bottomPlane, domainWidth, domainDepth);

                        int[] arrayPaths = { b, w, d };
                        GH_Path path = new GH_Path(arrayPaths);
                        moduleTree.Add(module, path);
                    }
   
                }

            }



            DA.SetDataTree(0, moduleTree);
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