using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;

namespace DSVcommon
{
    public class DesignVector
    {
        public int ID { get; set; }
        public List<double> DesignMap { get; set; }
        public List<double> ObjValues { get; set; }
        public static int DesignMapSize { get; set; }
        public static int ObjValuesSize { get; set; }
        public static List<string> DesignMapLabels { get; set; }
        public static List<string> ObjValuesLabels { get; set; }

        public DesignVector() { } // basic constructor

        public DesignVector
            (List<double> designMap) // constructor for Reference Vector: no ID or Objective Values
        {
            this.DesignMap = designMap;
        }
        public DesignVector
            (int id, List<double> designMap, List<double> objValues) // constructor for DV with Design Map and Objective Values
        {
            this.ID = id;
            this.DesignMap = designMap;
            this.ObjValues = objValues;
        }

        /// <summary>
        /// Returns euclidean distance between two DesignVectors
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static double GetVectorDistance 
            (DesignVector v1, DesignVector v2)
        {
            double squaredDistance = 0;
            // iterate each dimension in the Design Map for calculating vector component squared distances
            for (int i = 0; i < v1.DesignMap.Count; i++)
                squaredDistance += Math.Pow (v1.DesignMap[i] - v2.DesignMap[i], 2);
            return Math.Sqrt(squaredDistance);
        }

        /// <summary>
        /// Finds DV in a list of DVs that is closest to a reference DV.
        /// </summary>
        /// <param name="designSpace"></param>
        /// <param name="refVector"></param>
        /// <param name="closestDistance"></param>
        /// <returns></returns>
        public static DesignVector FindClosestVector 
            (List<DesignVector> designSpace, DesignVector refVector, out double closestDistance)
        {
            DesignVector closestVector = designSpace[0];
            closestDistance = GetVectorDistance(closestVector, refVector);

            foreach (DesignVector currVector in designSpace) // already did 0...
            {
                // find closest distance - use sort?
                double currDistance = GetVectorDistance(currVector, refVector);
                if (currDistance < closestDistance)
                {
                    closestDistance = currDistance;
                    closestVector = currVector;
                }
            }
            return closestVector;
        }
        /// <summary>
        /// Returns a string describing the DesignVector
        /// </summary>
        /// <returns></returns>
        public string DescribeVector()
        {
            string description = "";
            description += "ID: " + ID.ToString() + "\n";

            for (int i = 0; i < DesignMap.Count; i++)
                description += DesignMapLabels[i] + ": " + DesignMap[i].ToString() + "\n";
            for (int j = 0; j < ObjValues.Count; j++)
                description += ObjValuesLabels[j] + ": " + ObjValues[j].ToString() + "\n";
            
            return description;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="varyingDimensions"></param>
        /// <param name="designSpace"></param>
        /// <param name="referenceVectorComponents"></param>
        /// <param name="closestVector">out parameter - </param>
        /// <returns></returns>
        public static List<DesignVector> SectionDesignSpace
            (List<int> varyingDimensions, List<DesignVector> designSpace, List<double> referenceVectorComponents, out DesignVector closestVector)
        {
            // find vector in DS closest to refVector
            DesignVector referenceVector = new DesignVector(referenceVectorComponents);
            closestVector = DesignVector.FindClosestVector(designSpace, referenceVector, out _);

            // determine fixed dimensions from varied - move to DSVcommon?
            List<double> fixedDimensions = new List<double>();
            double numberOfDimensions = DesignVector.DesignMapSize;
            for (int dim = 0; dim < numberOfDimensions; dim++)
                if (!varyingDimensions.Contains(dim)) fixedDimensions.Add(dim);

            // find vectors in section, i.e., that have ALL the same values in the Fixed Dimensions as the reference vector
            List<DesignVector> sectionedSpace = new List<DesignVector>();
            foreach (DesignVector dv in designSpace)
            {
                List<bool> inSection = new List<bool>();
                foreach (int fixedDimension in fixedDimensions)
                {
                    if (dv.DesignMap[fixedDimension] == referenceVector.DesignMap[fixedDimension]) inSection.Add(true);
                    else inSection.Add(false);
                }
                // if inSection only contains Trues, then add vector to sectioned space
                if (!inSection.Contains(false)) sectionedSpace.Add(dv);
            }

            return sectionedSpace;
        }

        public static List<Point3d> Make3DChart // change name eventually...
            (List<DesignVector> designSpace, List<DesignVector> sectionedSpace, List<int> varyingDimensions, int resultToMap, out BoundingBox sectionedSpaceBox)
        {
            // corrected Result int, from obsolete GH implementation - to be improved
            int corrResultToMap = resultToMap - sectionedSpace[0].DesignMap.Count;

            // make 3D points from DVs in sectioned Space
            List<Point3d> sectionedSpacePoints = new List<Point3d>(); // main output?
            foreach (DesignVector dv in sectionedSpace)
            {
                List<double> pointCoords = new List<double>();
                foreach (int dim in varyingDimensions)
                    pointCoords.Add(dv.DesignMap[dim]);        
                pointCoords.Add(dv.ObjValues[corrResultToMap]);
                sectionedSpacePoints.Add (new Point3d(pointCoords[0], pointCoords[1], pointCoords[2]));
            }

            // make bounding box for sectioned space
            sectionedSpaceBox = new BoundingBox(sectionedSpacePoints);

            // get domain of resultToMap in the whole design space, not only sectioned space
            // if for sectionedSpace, use it in GetDimensionValues

            GetDimensionValues(designSpace, resultToMap, out Interval resultsDomain, out _) ;


            sectionedSpaceBox = new BoundingBox
                (sectionedSpaceBox.Min.X, sectionedSpaceBox.Min.Y, resultsDomain.Min,
                sectionedSpaceBox.Max.X, sectionedSpaceBox.Max.Y, resultsDomain.Max);

            return sectionedSpacePoints;

        }

        public static List<GeometryBase> MakeChartAxes
            (BoundingBox chartBox, List<DesignVector> designSpace, List<int> varyingDimensions, out List<TextDot> axesTextDots)
        {
            List<GeometryBase> axesElements = new List<GeometryBase>();
            // axesTextDots need to be separate, to be displayed directly in viewport
            axesTextDots = new List<TextDot>();
            // make axes lines
            Curve xAxis = new Line(chartBox.Min, new Vector3d(chartBox.Diagonal.X * Vector3d.XAxis)).ToNurbsCurve();
            Curve yAxis = new Line(chartBox.Min, new Vector3d(chartBox.Diagonal.Y * Vector3d.YAxis)).ToNurbsCurve();
            Curve zAxis = new Line(chartBox.Min, new Vector3d(chartBox.Diagonal.Z * Vector3d.ZAxis)).ToNurbsCurve();

            axesElements.AddRange(new List<GeometryBase>() { xAxis, yAxis, zAxis });

            // this should be refactored in a loop
            GetDimensionValues(designSpace, varyingDimensions[0], out Interval xAxisDomain, out string xDimName);
            axesElements.AddRange(MakeSingleAxis(xAxis, xAxisDomain, xDimName, out List<TextDot> xAxisTextDots));
            axesTextDots.AddRange(xAxisTextDots);

            GetDimensionValues(designSpace, varyingDimensions[1], out Interval yAxisDomain, out string yDimName);
            axesElements.AddRange(MakeSingleAxis(yAxis, yAxisDomain, yDimName, out List<TextDot> yAxisTextDots));
            axesTextDots.AddRange(yAxisTextDots);

            GetDimensionValues(designSpace, varyingDimensions[2], out Interval zAxisDomain, out string zDimName);
            axesElements.AddRange(MakeSingleAxis(zAxis, zAxisDomain, zDimName, out List<TextDot> zAxisTextDots));
            axesTextDots.AddRange(zAxisTextDots);

            return axesElements;
        }

        public static List<GeometryBase> MakeSingleAxis(Curve axisLine, Interval axisDomain, string axisName, out List<TextDot> axisTextDots)
        {
            int nrDivs = 7; // make dependent on nr vectors per dimension...
            double axisDotLength = .2;
            int dp = 2; // nr of decimal places in axis text dots

            //customize string formatting mask for variable number of decimal places
            string formatMask = "{0:0.";
            for (int i = 0; i < dp; i++) formatMask += "0";
            formatMask += "}";

            
            List<GeometryBase> axisElements = new List<GeometryBase>();

            // divide axes lines and assign values - based on the whole Design Space
            Plane[] divPlanes = axisLine.GetPerpendicularFrames(axisLine.DivideByCount(nrDivs - 1, true));

            // text dots cannot be output as geometry, rather displayed directly in viewport
            List<Curve> axisDots = new List<Curve>();
            axisTextDots = new List<TextDot>();
            double step = axisDomain.Min; // for iterating axis values

            foreach (Plane divPlane in divPlanes)
            {
                Vector3d dotDir = new Vector3d(divPlane.XAxis * - axisDotLength);
                // adds a spoke to the axis line
                axisDots.Add(new Line(divPlane.Origin, dotDir).ToNurbsCurve());
                // adds a TextDot with the current axis value
                axisTextDots.Add(new TextDot (
                    string.Format(formatMask, step), // truncate to N decimal places
                    divPlane.Origin + dotDir * 2)); 
                // update axis value
                step += axisDomain.Length / (nrDivs - 1);
            }

            axisElements.AddRange(axisDots);

            // add axis name
            axisTextDots.Add(new TextDot(axisName, axisLine.PointAtEnd + axisLine.TangentAtEnd * axisDotLength* 2));

            return axisElements;
        }

        public static List<Point3d> MapDesignVectorsToChart (List<Point3d> points, BoundingBox sourceBox, BoundingBox targetBox)
        {
            List<Point3d> transformedPoints = new List<Point3d>();

            // define origin plane of sourceBox
            Plane sourceOrigin = Plane.WorldXY;
            sourceOrigin.Origin = sourceBox.Min;

            // create Transforms for recreating BoxMap component
            Transform boxScaling = Transform.Scale(sourceOrigin,
                targetBox.Diagonal.X / sourceBox.Diagonal.X, 
                targetBox.Diagonal.Y / sourceBox.Diagonal.Y, 
                targetBox.Diagonal.Z / sourceBox.Diagonal.Z);
            Transform boxMoving = Transform.Translation(new Vector3d(targetBox.Min) - new Vector3d (sourceBox.Min));
            Transform boxMapping = Transform.Multiply(boxMoving, boxScaling);

            foreach (Point3d point in points)
            {
                Point3d transformedPoint = new Point3d(point);
                transformedPoint.Transform(boxMapping);
                transformedPoints.Add(transformedPoint);
            }

            return transformedPoints;
        }

        public static Mesh MeshChartPoints(List<Point3d> pts)
        {
            // code from James Ramsden: http://james-ramsden.com/create-2d-delaunay-triangulation-mesh-with-c-in-grasshopper/
            // with help from Daniel Piker: https://discourse.mcneel.com/t/3d-delaunay/126194

            //convert point3d to node2
            //grasshopper requres that nodes are saved within a Node2List for Delaunay
            var nodes = new Grasshopper.Kernel.Geometry.Node2List();
            for (int i = 0; i < pts.Count; i++)
                //notice how we only read in the X and Y coordinates
                //  this is why points should be mapped onto the XY plane
                nodes.Append(new Grasshopper.Kernel.Geometry.Node2(pts[i].X, pts[i].Y));

            //solve Delaunay
            var delMesh = new Mesh();
            var faces = new List<Grasshopper.Kernel.Geometry.Delaunay.Face>();

            faces = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Faces(nodes, 0);

            //output
            delMesh = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 0, ref faces);
            for (int i = 0; i < pts.Count; i++)
                delMesh.Vertices.SetVertex(i, pts[i]);
            return delMesh;
        }


        /// <summary>
        /// Extracts a "row" in the design space corresponding to a specific dimension
        /// Should be using an array to encode a design space...
        /// </summary>
        /// <param name="designSpace"></param>
        /// <param name="dimension"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static List<double> GetDimensionValues (List<DesignVector> designSpace, int dimension, out Interval domain, out string dimName)
        {
            List<double> allValues = new List<double>();

            // select between design map or obj values
            if (dimension < DesignVector.DesignMapSize)
            {
                foreach (DesignVector dv in designSpace)
                    allValues.Add(dv.DesignMap[dimension]);
                dimName = DesignMapLabels[dimension];
            }


            else
            {
                int corrDimension = dimension - DesignVector.DesignMapSize;
                foreach (DesignVector dv in designSpace)
                    allValues.Add(dv.ObjValues[corrDimension]);
                dimName = ObjValuesLabels[corrDimension];
            }    


            domain = new Interval(allValues.Min(), allValues.Max());
            return allValues;
        }

    }
}
