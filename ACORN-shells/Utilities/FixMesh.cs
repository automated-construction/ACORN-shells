using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Karamba.Geometry;
using Karamba.GHopper.Models;
using Karamba.Results;
using Karamba.Models;
using Karamba.GHopper.Geometry;
using Karamba.Algorithms;
using Grasshopper.Kernel.Geometry.Delaunay;
using Rhino.Runtime.InteropWrappers;
using Rhino.Render.Fields;
using System.Windows.Forms;
using System.Xml;
using Karamba.Utilities.AABBTrees;

namespace ACORN_shells
{
    /// <summary>
    /// Should render AnalysisResults and AnalysisResultsExternal obsolete
    /// Only uses AnalyzeThI if FAST is on, and if it does not work reverts back to ThII
    /// Change name after testing
    /// </summary>
    public class FixMesh : GH_Component
    {
        public FixMesh()
          : base("FixMesh", "A:FixMesh",
              "Fixes mesh for FEM",
              "ACORN Shells", " Utilities")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }


        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Target aspect ratio", "A", "Target aspect ratio", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Incircles", "I", "Incircles for each mesh face", GH_ParamAccess.list);
            pManager.AddNumberParameter("Excircles", "E", "Excircles for each mesh face", GH_ParamAccess.list);
            pManager.AddNumberParameter("Aspect ratios", "A", "Aspect ratios", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = new Mesh();
            double targetAspectRatio = double.NaN;

            if (!DA.GetData(0, ref mesh)) return;
            if (!DA.GetData(1, ref targetAspectRatio)) return;

            List<Circle> incircles = new List<Circle>();
            List<Circle> excircles = new List<Circle>();
            List<double> aspectRatios = new List<double>();

            Mesh fixedMesh = mesh.Duplicate() as Mesh;

            foreach (MeshFace f in fixedMesh.Faces)
            {
                Point3d pA = fixedMesh.Vertices[f.A];
                Point3d pB = fixedMesh.Vertices[f.B];
                Point3d pC = fixedMesh.Vertices[f.C];
                // make a triangle3D from face edges 
                Triangle triangle = new Triangle();
                //Triangle3d faceTriangle = new Rhino.Geometry.Triangle3d(pA, pB, pC);
                // get incircle (circumcircle?) and excircle ()
                //Circle incircle = faceTriangle.Circumcircle;
                Circle incircle = new Circle(pA, pB, pC);
                Circle excircle = new Circle(pA, pB, pC);
                // aspect ratio  = 2RI/RO
                // http://support.moldex3d.com/r15/en/modelpreparation_reference-pre_meshqualitydefinition.html
                double aspectRatio = 2 * incircle.Radius / excircle.Radius;
                incircles.Add(incircle);
                excircles.Add(excircle);
                aspectRatios.Add(aspectRatio);

            }

            DA.SetDataList(0, incircles);
            DA.SetDataList(0, excircles);
            DA.SetDataList(0, aspectRatios);





        }

        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                return ACORN_shells.Properties.Resources.fixMesh;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("a7923690-13da-436b-96c7-84e17174c3d1"); }
        }
    }
}
