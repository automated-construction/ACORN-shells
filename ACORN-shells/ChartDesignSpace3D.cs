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
using Grasshopper.Kernel.Types;
using DSVcommon;

namespace ACORN_shells
{
    /// <summary>
    /// Sections a design space from DSE
    /// 
    /// NOT BEING COMPILED
    /// </summary>
    public class ChartDesignSpace3D : GH_Component
    {
        public ChartDesignSpace3D()
          : base("Chart Design Space 3D", "A:ChartDS-3D",
              "Charts a Design Space in 3D according to fixed dimensions",
              "ACORN", "DSV")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Varying dimensions", "VD", "Varying dimensions that define section", GH_ParamAccess.list);
            pManager.AddGenericParameter("Design space", "DS", "Design Vectors in the design space", GH_ParamAccess.list);
            pManager.AddNumberParameter("Reference vector components", "RVC", "Reference vector components", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Result to map", "R", "Result to map", GH_ParamAccess.item);
            pManager.AddBoxParameter("Chart Box", "B", "Box containing the chart", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Design Vectors in section", "S", "Design Vectors in section", GH_ParamAccess.list);
            pManager.AddGenericParameter("Closest Design Vector", "C", "Design Vector closest to reference vector", GH_ParamAccess.item);
            pManager.AddTextParameter("Closest Vector Info", "I", "Information on closest vector", GH_ParamAccess.item);
            pManager.AddPointParameter("Sectioned space points", "P", "Points corresponding to sectioned Design Vectors", GH_ParamAccess.list);
            pManager.AddBoxParameter("Space Box", "B", "Box containing Design Space points", GH_ParamAccess.item);
            pManager.AddPointParameter("Mapped points", "M", "Points corresponding to mapped sectioned Design Vectors", GH_ParamAccess.list);
            pManager.AddGeometryParameter("Chart axes", "A", "Chart axes", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            List<int> varyingDimensions = new List<int>();
            List<DesignVector> designSpace = new List<DesignVector>();
            List<double> referenceVectorComponents = new List<double>();
            int resultToMap = 0;
            Box ghChartBox = Box.Unset;

            if (!DA.GetDataList(0, varyingDimensions)) return;
            if (!DA.GetDataList(1, designSpace)) return;
            if (!DA.GetDataList(2, referenceVectorComponents)) return;
            if (!DA.GetData(3, ref resultToMap)) return; 
            if (!DA.GetData(4, ref ghChartBox)) return;

            // convert Box to BoundingBox
            BoundingBox chartBox = ghChartBox.BoundingBox;

            List<DesignVector> sectionedSpace = DesignVector.SectionDesignSpace
                (varyingDimensions, designSpace, referenceVectorComponents, out DesignVector closestVector);

            string closestVectorInfo = closestVector.DescribeVector();

            List<Point3d> designSpacePoints = DesignVector.Make3DChart
                (designSpace, sectionedSpace, varyingDimensions, resultToMap, out BoundingBox spaceBox);

            List<Point3d> mappedPoints = DesignVector.MapDesignVectorsToChart(designSpacePoints, spaceBox, chartBox);

            List<GeometryBase> axesElements = DesignVector.MakeChartAxes(chartBox, designSpace, varyingDimensions);

            DA.SetDataList(0, sectionedSpace);
            DA.SetData(1, closestVector);
            DA.SetData(2, closestVectorInfo);
            DA.SetDataList(3, designSpacePoints);
            DA.SetData(4, spaceBox);
            DA.SetDataList(5, mappedPoints); 
            DA.SetDataList(6, axesElements);
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
            get { return new Guid("dd1fe2e9-65e0-4e37-8ae0-dbf0b0f1789c"); }
        }
    }
}