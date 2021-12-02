using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;

namespace ACORN_shells
{
    public class Segment : GH_Component
    {
        public Segment()
          : base("Segment", "A:Segment",
              "Segment shell using stress lines.",
              "ACORN Shells", "Segmentation")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell", "S", "Shell Brep.", GH_ParamAccess.item);
            pManager.AddCurveParameter("StressLines1", "SL1", "Stress lines related to tension.", GH_ParamAccess.list);
            pManager.AddCurveParameter("StressLines2", "SL2", "Stress lines related to compression.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Segments", "SEG", "Segmented shell pieces.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep shell = null;
            List<Curve> stressLines1 = new List<Curve>();
            List<Curve> stressLines2 = new List<Curve>();

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetDataList(1, stressLines1)) return;
            if (!DA.GetDataList(2, stressLines2)) return;

            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // Pull stress lines to shell
            stressLines1 = stressLines1.SelectMany(l => Curve.ProjectToBrep(l, shell, Vector3d.ZAxis, fileTol)).ToList();
            stressLines2 = stressLines2.SelectMany(l => Curve.ProjectToBrep(l, shell, Vector3d.ZAxis, fileTol)).ToList();

            // Intersect with each stress line sets and retrieve polyline
            var segmentLines = new List<Curve>();
            var points1 = new Dictionary<int, List<double>>();
            var points2 = new Dictionary<int, List<double>>();

            for(int i = 0; i < stressLines1.Count; i++)
            {
                points1[i] = new List<double>();
                var s1 = stressLines1[i];
                for (int j = 0; j < stressLines2.Count; j++)
                {
                    if (!points2.ContainsKey(j))
                        points2[j] = new List<double>();
                    var s2 = stressLines2[j];
                    var intersections = Rhino.Geometry.Intersect.Intersection.CurveCurve(s1, s2, fileTol, fileTol);
                    points1[i].AddRange(intersections.Select(x => x.ParameterA));
                    points2[j].AddRange(intersections.Select(x => x.ParameterB));
                }
            }

            var intersectionPoints = new List<Point3d>();

            foreach(var kvp in points1)
            {
                kvp.Value.Sort();
                var points = kvp.Value.Select(t => stressLines1[kvp.Key].PointAt(t)).ToList();
                intersectionPoints.AddRange(points);
                segmentLines.Add(new PolylineCurve(points));
            }

            foreach (var kvp in points2)
            {
                kvp.Value.Sort();
                var points = kvp.Value.Select(t => stressLines2[kvp.Key].PointAt(t)).ToList();
                segmentLines.Add(new PolylineCurve(points));
            }

            // Make keystone
            var areaMassProp = AreaMassProperties.Compute(shell);
            var centroid = areaMassProp.Centroid;

            var wires = shell.GetWireframe(-1);
            var numEdges = wires.Count() / 2;

            var pointCloud = new PointCloud(intersectionPoints);

            var keystonePoints = new List<Point3d>();
            for(int i = 0; i < numEdges; i++)
            {
                var idx = pointCloud.ClosestPoint(centroid);
                var p = pointCloud[idx].Location;
                keystonePoints.Add(p);
                pointCloud.RemoveAt(idx);
            }

            // Order keystone points
            var translatedKPoints = keystonePoints.Select(p => p - centroid).ToList();
            var sortingCriteria = new List<double>();
            foreach(var p in translatedKPoints)
            {
                var mag = Math.Sqrt(p.X*p.X + p.Y*p.Y);
                if (p.Y > 0)
                    sortingCriteria.Add(Math.Acos(p.X / mag));
                else
                    sortingCriteria.Add(2 * Math.PI - Math.Acos(p.X / mag));
            }
            var sortIndex = sortingCriteria
                .Select((x, i) => new KeyValuePair<int, double>(i, x))
                .OrderBy(x => x.Value)
                .Select(x => x.Key);
            keystonePoints = sortIndex.Select(i => keystonePoints[i]).ToList();

            keystonePoints.Add(keystonePoints[0]);
            segmentLines.AddRange(Curve.ProjectToBrep(new PolylineCurve(keystonePoints), shell, Vector3d.ZAxis, fileTol));

            // Segment
            var segments = shell.Faces[0].Split(segmentLines, fileTol);
            var segmentBreps = Enumerable.Range(0, segments.Faces.Count)
                .Select(f => segments.Faces.ExtractFace(f))
                .ToList();

            DA.SetDataList(0, segmentBreps);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.segment;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("79bca3ce-5149-4b9b-8862-c7e832eff071"); }
        }
    }
}