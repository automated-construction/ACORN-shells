using System;
using System.Drawing;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using Grasshopper;

using Karamba.Geometry;
using Karamba.Elements;
using Karamba.CrossSections;
using Karamba.GHopper.Geometry;
using Karamba.GHopper.Elements;
using Karamba.GHopper.CrossSections;

namespace ACORN_shells
{
    /// <summary>
    /// Creates Karamba spring beam from a line
    /// </summary>
    public class LineToSpring : GH_Component
    {
        public LineToSpring()
          : base("Line to Spring", "LineToSpring",
              "Creates Karamba spring beam from a GH line",
              "ACORN Shells", "  Shape")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Line", "L", "Line", GH_ParamAccess.item);
            pManager.AddNumberParameter("Gap size", "G", "Distance between segments (optional, for visualisation)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Springs Cross Section", "CS", "Karamba spring cross section", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Karamba Springs", "K", "Spring elements for Karamba", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance; //smaller tollerance works!

            Line springLine = new Line();
            double gapSize = double.NaN;
            GH_CrossSection ghSection = null;

            if (!DA.GetData(0, ref springLine)) return;
            if (!DA.GetData(1, ref gapSize)) return;
            if (!DA.GetData(2, ref ghSection)) return;

            var logger = new Karamba.Utilities.MessageLogger();
            var k3dKit = new KarambaCommon.Toolkit(); // for Builders

            CroSec_Spring k3dSection = (CroSec_Spring)ghSection.Value;


            var k3dSpring = k3dKit.Part.LineToBeam(new List<Line3>() { springLine.Convert() },
                            new List<string>() { "ACORNSPRING" },
                            new List<CroSec>() { k3dSection },
                            logger, out _, true, gapSize/2);

            var ghSpring = new GH_Element(k3dSpring[0]);



            DA.SetData(0, ghSpring);

        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.lineSpring;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("f5731194-8cb2-4004-9b46-01aca1815e17"); }
        }


        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.tertiary; }
        }

    }
}