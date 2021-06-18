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
            double numberOfDimensions = designSpace[0].DesignMap.Count;
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
            // for extending bounding box so that charts are consistent for the same result
            List<double> allValuesForResult = new List<double>();
            foreach (DesignVector dv in designSpace)             
                allValuesForResult.Add(dv.ObjValues[corrResultToMap]);
            sectionedSpaceBox = new BoundingBox
                (sectionedSpaceBox.Min.X, sectionedSpaceBox.Min.Y, allValuesForResult.Min(),
                sectionedSpaceBox.Max.X, sectionedSpaceBox.Max.Y, allValuesForResult.Max());

            return sectionedSpacePoints;

        }

        public static List<GeometryBase> MakeChartAxes(BoundingBox chartBox, List<DesignVector> designSpace, List<int> varyingDimensions)
        {
            List<GeometryBase> axesElements = new List<GeometryBase>();
            // make axes lines
            Curve xAxis = new Line(chartBox.Min, new Vector3d(chartBox.Diagonal.X * Vector3d.XAxis)).ToNurbsCurve();
            Curve yAxis = new Line(chartBox.Min, new Vector3d(chartBox.Diagonal.Y * Vector3d.YAxis)).ToNurbsCurve();
            Curve zAxis = new Line(chartBox.Min, new Vector3d(chartBox.Diagonal.Z * Vector3d.ZAxis)).ToNurbsCurve();

            axesElements.AddRange(new List<GeometryBase>() { xAxis, yAxis, zAxis });

            // set axes domains from varying dimensions and result -- repeats a lot (see Make3D chart ln 152)
            List<double> allValuesForX = new List<double>();
            foreach (DesignVector dv in designSpace)
                allValuesForX.Add(dv.ObjValues[varyingDimensions[0]]);
            Interval xAxisDomain = new Interval(allValuesForX.Min(), allValuesForX.Max());

            axesElements.AddRange(MakeSingleAxis(xAxis, xAxisDomain));
            //axesElements.AddRange(MakeSingleAxis(yAxis));
            //axesElements.AddRange(MakeSingleAxis(zAxis));

            return axesElements;
        }

        public static List<GeometryBase> MakeSingleAxis(Curve axisLine, Interval axisDomain)
        {
            int nrDivs = 7; // make dependent on nr vectors per dimension...
            double axisDotLength = .05;
            
            List<GeometryBase> axisElements = new List<GeometryBase>();

            // divide axes lines and assign values - based on the whole Design Space
            Plane[] divPlanes = axisLine.GetPerpendicularFrames(axisLine.DivideByCount(nrDivs - 1, true));

            List<Curve> axisDots = new List<Curve>();
            List<TextDot> axisTextDots = new List<TextDot>();
            double step = axisDomain.Min; // for iterating axis values

            foreach (Plane divPlane in divPlanes)
            {
                // adds a spoke to the axis line
                axisDots.Add(new Line(divPlane.Origin, new Vector3d(divPlane.XAxis * -axisDotLength)).ToNurbsCurve());
                // adds a TextDot with the current axis value
                axisTextDots.Add(new TextDot (step.ToString(), divPlane.PointAt(-axisDotLength, 0))); 
                // update axis value
                step += axisDomain.Length / (nrDivs - 1);
            }
            axisElements.AddRange(axisDots);
            axisElements.AddRange(axisTextDots);

            return axisElements;
        }

        public static List<Point3d> MapDesignVectorsToChart (List<Point3d> points, BoundingBox sourceBox, BoundingBox targetBox)
        {
            List<Point3d> transformedPoints = new List<Point3d>();

            // create Transform for recreating BoxMap component
            /*
            Transform boxScaling = Transform.ChangeBasis
                (Vector3d.XAxis * sourceBox.Diagonal.X, Vector3d.YAxis * sourceBox.Diagonal.Y, Vector3d.ZAxis * sourceBox.Diagonal.Z,
                Vector3d.XAxis * targetBox.Diagonal.X, Vector3d.YAxis * targetBox.Diagonal.Y, Vector3d.ZAxis * targetBox.Diagonal.Z);
            */

            // define origin plane of sourceBox
            Plane sourceOrigin = Plane.WorldXY;
            sourceOrigin.Origin = sourceBox.Min;

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

    }
}
