﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Karamba.GHopper.Geometry;
using Karamba.Geometry;
using System.Linq;

namespace ACORN_shells
{
    public class StressLines : GH_Component
    {
        // Karamba defaults for stress line extraction
        double A_TOL = 5 / 180 * Math.PI;
        int MAX_ITER = 500;

        public StressLines()
          : base("StressLines", "A:StressLines",
              "Generate stress lines and cable profiles from analysed Karamba model.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Analysed Karamba model.", GH_ParamAccess.item);
            pManager.AddBrepParameter("Shell", "S", "Shell Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("KeystoneWidth", "KW", "Width of keystone.", GH_ParamAccess.item);
            pManager.AddNumberParameter("CornerstoneWidth", "CW", "Width of cornerstone.", GH_ParamAccess.item);
            pManager.AddNumberParameter("LengthParam1", "L1", "Distance between stress lines 1.", GH_ParamAccess.item);
            pManager.AddGenericParameter("LengthParam2", "L2", "Distance between stress lines 2.", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Corners", "C", "Corner curves of shell.", GH_ParamAccess.list);
            //pManager.AddCurveParameter("Edges", "E", "Edge curves of shell.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("StressLines1", "SL1", "Stress lines related to tension.", GH_ParamAccess.list);
            pManager.AddCurveParameter("StressLines2", "SL2", "Stress lines related to compression.", GH_ParamAccess.list);
            pManager.AddCurveParameter("CableProfiles1", "CP1", "Cable profiles related to tension.", GH_ParamAccess.list);
            pManager.AddCurveParameter("CableProfiles2", "CP2", "Cable profiles related to compression.", GH_ParamAccess.list);
            //pManager.AddCurveParameter("Extracted corners", "EC", "Extracted corners (for testing).", GH_ParamAccess.list);
            //pManager.AddCurveParameter("Extracted edges", "EE", "Extracted edges (for testing).", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Karamba.GHopper.Models.GH_Model ghModel = null;
            Brep shell = null;
            //List<Curve> corners = new List<Curve>();
            //List<Curve> edges = new List<Curve>();
            double keystoneWidth = 0;
            double cornerstoneWidth = 0;
            double lengthParam1 = 0;
            double lengthParam2 = 0;

            if (!DA.GetData(0, ref ghModel)) return;
            if (!DA.GetData(1, ref shell)) return;
            if (!DA.GetData(2, ref keystoneWidth)) return;
            if (!DA.GetData(3, ref cornerstoneWidth)) return;
            if (!DA.GetData(4, ref lengthParam1)) return;
            if (!DA.GetData(5, ref lengthParam2)) return;
            //if (!DA.GetDataList(6, corners)) return;
            //if (!DA.GetDataList(7, edges)) return;

            var model = ghModel.Value;

            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // Find centroid of shell
            var areaMassProp = AreaMassProperties.Compute(shell);
            var centroid = areaMassProp.Centroid;

            // extract corners from surface, corners being the 4 shortest boundary edges, instead of being an input
            // should go to SHELLScommon, if it ever exists
            var shellAllEdges = shell.Edges;
            // sort edges by length
            List<BrepEdge> sortedAllEdges = shellAllEdges.OrderBy(s => s.GetLength()).ToList();
            // get 50% shortest edges
            var cornersE = new List<Curve>(); // removeE
            var edgesE = new List<Curve>(); // removeE
            int numAllEdges = sortedAllEdges.Count;
            for (int i = 0;               i < numAllEdges / 2; i++) cornersE.Add(sortedAllEdges[i].EdgeCurve); // equivalent to GetRange(0,4)
            for (int i = numAllEdges / 2; i < numAllEdges;    i++) edgesE.Add(sortedAllEdges[i].EdgeCurve);

            // Calculate points to analyse stress lines at
            var edgeMidpoints = edgesE.Select(e => e.PointAtNormalizedLength(0.5)).ToList();
            var cornerMidpoints = cornersE.Select(c => c.PointAtNormalizedLength(0.5)).ToList();
            var wires = shell.GetWireframe(-1);
            var wirePoints = new PointCloud(wires.Select(w => w.PointAtNormalizedLength(0.5)));
            edgeMidpoints = edgeMidpoints.Select(e => wirePoints[wirePoints.ClosestPoint(e)].Location).ToList();
            cornerMidpoints = cornerMidpoints.Select(c => wirePoints[wirePoints.ClosestPoint(c)].Location).ToList();

            // Use shortest path to get edge generating lines
            var keystonePoints = new List<Point3d>();
            var edgeGenLines = new List<Curve>();

            var shellSurf = shell.Surfaces[0];
            double u, v;
            shellSurf.ClosestPoint(centroid, out u, out v);
            var centroidUV = new Point2d(u, v);
            foreach(var p in edgeMidpoints)
            {
                shellSurf.ClosestPoint(p, out u, out v);
                var pUV = new Point2d(u, v);
                var line = shellSurf.ShortPath(centroidUV, pUV, fileTol);
                line = line.Trim(CurveEnd.Start, keystoneWidth / 2 * Math.Sqrt(2));
                keystonePoints.Add(line.PointAtNormalizedLength(0));
                edgeGenLines.Add(line);
            }

            // Get keystone polylinecurve to trim corner generating lines

            // because corners are extracted from surface, sort keystonePoints by polar coordinates (rho angle)
            // detemine shell center for relative polar coordinates of keystone points - move to SHELLScommon
            Point3d shellCenter = shell.GetBoundingBox(false).Center;
            List<PointAndAngle> keystonePointsToOrder = new List<PointAndAngle>();

            foreach (Point3d point in keystonePoints)
            {
                // get face center polar coordinate - move to SHELLScommon?
                Vector3d orientation = new Vector3d(point) - new Vector3d(shellCenter);
                double angle = Math.Atan2(orientation.Y, orientation.X);
                //if (angle < 0) angle += Math.PI * 2; // ensures angle always positive, [0, 2Pi] 
                keystonePointsToOrder.Add(new PointAndAngle { Point = point, Angle = angle });
            }

            // sort by angle
            keystonePointsToOrder = keystonePointsToOrder.OrderBy(k => k.Angle).ToList();
            keystonePoints = keystonePointsToOrder.Select(k => k.Point).ToList();

            keystonePoints.Add(keystonePoints[0]);
            Curve keystonePoly = new PolylineCurve(keystonePoints);
            keystonePoly = Curve.ProjectToBrep(keystonePoly, shell, Vector3d.ZAxis, fileTol)[0];

            // Use shortest path to get corner generating lines
            var cornerGenLines = new List<Curve>();
            foreach (var p in cornerMidpoints)
            {
                shellSurf.ClosestPoint(p, out u, out v);
                var pUV = new Point2d(u, v);
                var line = shellSurf.ShortPath(centroidUV, pUV, fileTol);
                line = line.Trim(CurveEnd.End, cornerstoneWidth);
                var intersect = Rhino.Geometry.Intersect.Intersection.CurveCurve(
                    line, keystonePoly, fileTol, fileTol)[0];
                var param_trim = intersect.ParameterA;
                line = line.Trim(intersect.ParameterA, 0);
                cornerGenLines.Add(line);
            }

            // Find source points
            var stressLineSources1 = new List<Point3d>();
            var cableLineSources1 = new List<Point3d>();
            foreach(var l in cornerGenLines)
            {
                var length = l.GetLength();
                int divs = (int)Math.Ceiling(length / lengthParam1);// Want ceil to use as upper bound
                var stressParams = Enumerable.Range(1, divs).Select(x => x / (double)divs).ToList();
                var cableParams = Enumerable.Range(0, divs).Select(x => (x + 0.5) / (double)divs).ToList();
                stressLineSources1.AddRange(stressParams.Select(t => l.PointAtNormalizedLength(t)));
                cableLineSources1.AddRange(cableParams.Select(t => l.PointAtNormalizedLength(t)));
            }

            var stressLineSources2 = new List<Point3d>();
            var cableLineSources2 = new List<Point3d>();
            foreach (var l in edgeGenLines)
            {
                var length = l.GetLength();
                int divs = (int)Math.Ceiling(length / lengthParam2);// Want ceil to use as upper bound
                var stressParams = Enumerable.Range(0, divs).Select(x => x / (double)divs).ToList();
                var cableParams = Enumerable.Range(0, divs).Select(x => (x + 0.5) / (double)divs).ToList();
                stressLineSources2.AddRange(stressParams.Select(t => l.PointAtNormalizedLength(t)));
                cableLineSources2.AddRange(cableParams.Select(t => l.PointAtNormalizedLength(t)));
            }

            // Get stress lines
            var stressLines1 = PrincipalStressLines(model, stressLineSources1).Item1;
            var stressLines2 = PrincipalStressLines(model, stressLineSources2).Item2;
            var cableProfiles1 = PrincipalStressLines(model, cableLineSources1).Item1;
            var cableProfiles2 = PrincipalStressLines(model, cableLineSources2).Item2;

            // Add line from centroid to edge to tension stress lines set
            foreach (var p in edgeMidpoints)
            {
                shellSurf.ClosestPoint(p, out u, out v);
                var pUV = new Point2d(u, v);
                stressLines1.Add(shellSurf.ShortPath(centroidUV, pUV, fileTol));
            }

            // Add edges to compression stress lines set
            foreach (var e in edgesE)
            {
                var pMid = e.PointAtNormalizedLength(0.5);
                var idx = wirePoints.ClosestPoint(pMid);
                stressLines2.Add(wires[idx]);
            }

            DA.SetDataList(0, stressLines1);
            DA.SetDataList(1, stressLines2);
            DA.SetDataList(2, cableProfiles1);
            DA.SetDataList(3, cableProfiles2);
            //DA.SetDataList(4, cornersE);
            //DA.SetDataList(5, edgesE);
        }

        private Tuple<List<Curve>, List<Curve>> PrincipalStressLines(Karamba.Models.Model model, List<Point3d> points)
        {
            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            var k3dPoints = points.Select(p => p.Convert()).ToList();
            var vivMesh = new Karamba.GHopper.Geometry.VivinityMesh(model);
            var sourceLines = new List<Line3>();
            foreach(var p in k3dPoints)
            {
                Line3 l;
                if(vivMesh.IntersectionLine(p, out l))
                    sourceLines.Add(l);
            }
            var result = new List<List<List<List<Line3>>>>();
            Karamba.Results.PrincipalStressLines.solve(model,
                0, sourceLines, fileTol, A_TOL, MAX_ITER, model.superimpFacsStates, out result);

            // Unpack Karamba line results and create polylines from it
            var stressLines1 = new List<Curve>();
            foreach (var i in result[0])
            {
                var polySegments = new List<Curve>();
                foreach(var j in i)
                {
                    var lines = j.Select(l => l.Convert()).ToList();
                    var polyPoints = lines.Select(l => l.From).ToList();
                    polyPoints.Add(lines.Last().To);
                    polySegments.Add(new PolylineCurve(polyPoints));
                }
                var stressLine = Curve.JoinCurves(polySegments);
                stressLines1.AddRange(stressLine);
            }

            var stressLines2 = new List<Curve>();
            foreach (var i in result[1])
            {
                var polySegments = new List<Curve>();
                foreach (var j in i)
                {
                    var lines = j.Select(l => l.Convert()).ToList();
                    var polyPoints = lines.Select(l => l.From).ToList();
                    polyPoints.Add(lines.Last().To);
                    polySegments.Add(new PolylineCurve(polyPoints));
                }
                var stressLine = Curve.JoinCurves(polySegments);
                stressLines2.AddRange(stressLine);
            }

            return Tuple.Create(stressLines1, stressLines2);
        }

        public class PointAndAngle
        {
            public Point3d Point { get; set; }
            public double Angle { get; set; }
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
            get { return new Guid("8d387c45-1b18-4cc3-9596-c9c9a05a470c"); }
        }
    }
}