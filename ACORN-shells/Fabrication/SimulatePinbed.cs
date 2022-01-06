using System;
using System.Drawing;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using Grasshopper;

namespace ACORN_shells
{
    /// <summary>
    /// Simulates the pinbed mould.
    /// </summary>
    public class SimulatePinbed : GH_Component
    {
        public SimulatePinbed()
          : base("Simulate Pinbed", "A:SimPinbed",
              "Simulates the pinbed mould.",
              "ACORN Shells", " Fabrication")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell segments", "SS", "Shell segments", GH_ParamAccess.tree);
            pManager.AddRectangleParameter("Modules", "M", "Rectangles corresponding to modules", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Variable height", "VH", "Enables variable module height within same segment. Optional, default is false", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Selected segments", "S", "Operate on selected segments [optional, for testing; if no input, computes all segments]", GH_ParamAccess.list);
            pManager.AddNumberParameter("Maximum pin length", "ML", "Maximum pin length", GH_ParamAccess.item);

            pManager[2].Optional = true;
            pManager[3].Optional = true;

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell segments", "SS", "Shell segments (selected is S is True)", GH_ParamAccess.tree); // test
            pManager.AddBrepParameter("Extended segments", "ES", "Extended segments", GH_ParamAccess.tree); // test
            pManager.AddRectangleParameter("Modules", "M", "Modules", GH_ParamAccess.tree); // test main
            pManager.AddBoxParameter("Boxes", "B", "Boxes", GH_ParamAccess.tree); // test
            pManager.AddLineParameter("Pin axes", "PA", "Pin axes", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Pin heights", "PH", "Pin heights", GH_ParamAccess.tree);
            pManager.AddColourParameter("Pin colors", "PC", "Pin colors relative to heights", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Surface shell = null;
            GH_Structure<GH_Brep> ghSegmentTree = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Rectangle> ghModuleTree = new GH_Structure<GH_Rectangle>();
            bool variableHeights = false;
            List<int> selectedSegmentIndexes = new List<int>();
            double maxPinLength = 0;

            //if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetDataTree<GH_Brep>(0, out ghSegmentTree)) return;
            if (!DA.GetDataTree<GH_Rectangle>(1, out ghModuleTree)) return;
            DA.GetData(2, ref variableHeights);
            DA.GetDataList(3, selectedSegmentIndexes);
            if (!DA.GetData(4, ref maxPinLength)) return;



            // convert moduleTree: GH_Structure (Grasshopper) to DataTree (RhinoCommon)
            // separate convert from trim tree D=2
            // repeated, move to COMMON
            DataTree<Rectangle3d> modules = new DataTree<Rectangle3d>();
            foreach (GH_Path path in ghModuleTree.Paths)
            {
                GH_Rectangle ghModule = ghModuleTree.get_Branch(path)[0] as GH_Rectangle;
                Rectangle3d module = new Rectangle3d();
                GH_Convert.ToRectangle3d(ghModule, ref module, GH_Conversion.Both);
                modules.Add(module, path);
            }

            // extract only selected modules and segments - tree branch, sort of
            if (selectedSegmentIndexes.Count > 0)
            {
                DataTree<Rectangle3d> selectedModules = new DataTree<Rectangle3d>();
                foreach (GH_Path path in modules.Paths)
                {
                    int currSegmentIndex = path.Indices[0];
                    if (selectedSegmentIndexes.Contains(currSegmentIndex)) 
                        selectedModules.Add(modules.Branch(path)[0], path);
                }
                // replaces original lists
                modules = selectedModules;
            }



            // convert segmentTree: GH_Structure (Grasshopper) to DataTree (RhinoCommon)
            // separate convert from trim tree D=2
            // repeated, move to COMMON
            DataTree<Brep> segments = new DataTree<Brep>();
            foreach (GH_Path path in ghSegmentTree.Paths)
            {
                GH_Brep ghSegment = ghSegmentTree.get_Branch(path)[0] as GH_Brep;
                Brep segment = new Brep();
                GH_Convert.ToBrep(ghSegment, ref segment, GH_Conversion.Both);
                segments.Add(segment, path);
            }

            // extract only selected modules and segments - tree branch, sort of // ALSO repeated, ALSO move to common: problem with generic types?
            if (selectedSegmentIndexes.Count > 0)
            {
                DataTree<Brep> selectedSegments = new DataTree<Brep>();
                foreach (GH_Path path in segments.Paths)
                {
                    int currSegmentIndex = path.Indices[0];
                    if (selectedSegmentIndexes.Contains(currSegmentIndex))
                        selectedSegments.Add(segments.Branch(path)[0], path);
                }
                // replaces original lists
                segments = selectedSegments;
            }



            // ------------ extend segments --------------

            // extract original surface by untrimming one segment
            Brep singleSegment = segments.Branch(0)[0];
            Surface shell = singleSegment.Faces[0].UnderlyingSurface();

            // segments are extended for covering whole pinbed module, for shape continuity
            DataTree<Brep> extendedSegments = new DataTree<Brep>();
            foreach (GH_Path path in modules.Paths)
            {
                Rectangle3d currModule = modules.Branch(path)[0];
                //project module onto surface
                Curve projectedModule = 
                    Curve.JoinCurves(
                        Curve.ProjectToBrep(currModule.ToNurbsCurve(), shell.ToBrep(), currModule.Plane.ZAxis, DocumentTolerance())
                    )[0];

                //split shell using module projection - SLOW!!!
                Brep[] shellSplinters = shell.ToBrep().Split(new List<Curve> { projectedModule }, DocumentTolerance());
                // determine correct split result: sort by area
                Brep currExtendedSegment = shellSplinters.ToList<Brep>().OrderBy(o => o.GetArea()).ToList()[0];

                extendedSegments.Add(currExtendedSegment, path);
            }




            // ------------ adjust module heights --------------
            // adjust to extended segments; possibility of variable heights within same segment

            DataTree<Rectangle3d> adjustedModules = new DataTree<Rectangle3d>();
            DataTree<Box> segmentBoxes = new DataTree<Box>();


            if (variableHeights)
            {
                foreach (GH_Path path in extendedSegments.Paths)
                {
                    Brep currSegment = extendedSegments.Branch(path)[0];
                    // get plane from corresponding module
                    Rectangle3d currModule = modules.Branch(path)[0];
                    // get bounding box
                    Box segmentBox = new Box(currModule.Plane, currSegment);
                    segmentBoxes.Add(segmentBox, path);
                    // determine adjustment vector
                    Vector3d adjustmentVector = segmentBox.Plane.ZAxis * segmentBox.Z.T0;
                    // translating plane because Rectangle3d does not have a definition for Translate
                    Plane adjustedPlane = new Plane(currModule.Plane);
                    adjustedPlane.Translate(adjustmentVector);
                    Rectangle3d adjustedModule = new Rectangle3d(adjustedPlane, currModule.X, currModule.Y);
                    adjustedModules.Add(adjustedModule, path);
                }
            }
            else // constant height within segment, convoluted due to tree management
            {
                // trim tree D=2 to extended segments, to get all extended segments per pour
                DataTree<Brep> flatExtendedSegments = new DataTree<Brep>();
                foreach (GH_Path path in extendedSegments.Paths)
                {
                    // look at first index corresponding to segments
                    int currSegment = path.Indices[0];
                    // creates a branch to store the modules in current segment in flatExtendedSegments
                    GH_Path flatPath = new GH_Path(currSegment);
                    // gets corresponding item
                    Brep extendedSegment = extendedSegments.Branch(path)[0] as Brep;
                    flatExtendedSegments.Add(extendedSegment, flatPath);
                }
                
                //flatExtendedSegments = SHELLScommon.TrimTreeD2 (extendedSegments);

                // trim tree D=2 to modules, to get all modules per pour
                // same as previous block, suggests refactoring in SHELLScommon, but was having trouble with generic types
                DataTree<Rectangle3d> flatModules = new DataTree<Rectangle3d>();
                foreach (GH_Path path in extendedSegments.Paths)
                {
                    // look at first index corresponding to segments
                    int currSegment = path.Indices[0];
                    // creates a branch to store the modules in current segment in flatExtendedSegments
                    GH_Path flatPath = new GH_Path(currSegment);
                    // gets corresponding item
                    Rectangle3d module = modules.Branch(path)[0];
                    flatModules.Add(module, flatPath);
                }

                // calculate bounding boxes based on module planes
                DataTree<Vector3d> flatAdjustmentVectors = new DataTree<Vector3d>();

                foreach (GH_Path path in flatExtendedSegments.Paths)
                {
                    // join extended segments to make union box
                    List<Brep> segmentsToJoin = flatExtendedSegments.Branch(path) as List<Brep>;
                    Brep joinedSegment = Brep.JoinBreps(segmentsToJoin, DocumentTolerance())[0];
                    // get plane from corresponding module set (first module)
                    Rectangle3d module = flatModules.Branch(path)[0];
                    // get bounding box
                    Box flatSegmentBox = new Box(module.Plane, joinedSegment);
                    segmentBoxes.Add(flatSegmentBox, path);

                    // determine adjustment vector
                    Vector3d adjustmentVector = flatSegmentBox.Plane.ZAxis * flatSegmentBox.Z.T0;
                    flatAdjustmentVectors.Add(adjustmentVector, path);
                }

                // apply adjustment to each module in original rcModuleTree (unflattened)
                // similar to above! there must be a tree management library...
                foreach (GH_Path path in modules.Paths)
                {
                    // look at first index corresponding to segments
                    int currSegmentIndex = path.Indices[0];
                    // creates a branch to store the modules in current segment in flatExtendedSegments
                    GH_Path flatPath = new GH_Path(currSegmentIndex);
                    // read adjustment vector from datatree...
                    Vector3d currAdjustmentVector = flatAdjustmentVectors.Branch(flatPath)[0];
                    // read current module
                    Rectangle3d currModule = modules.Branch(path)[0];
                    // translating plane because Rectangle3d does not have a definition for Translate
                    Plane adjustedPlane = new Plane(currModule.Plane);
                    adjustedPlane.Translate(currAdjustmentVector);
                    Rectangle3d adjustedModule = new Rectangle3d(adjustedPlane, currModule.X, currModule.Y);
                    adjustedModules.Add(adjustedModule, path);


                }
            }

            // -------------- calculate pin heights ---------------
            // (takes adjustedModules and extendedSegments, in case we want to separate into smaller components)

            int pinsPerModuleX = 3; // make input?
            int pinsPerModuleY = 3; // make input?

            DataTree<Line> pinAxes = new DataTree<Line>();
            DataTree<Color> pinColors = new DataTree<Color>();
            //CustomDisplay pinDisplay = new CustomDisplay(true); // for coloring pinAxes

            DataTree<double> pinLengths = new DataTree<double>();
            // export list of 9 heights according to pin layout (CONFIRM)
            // 6 7 8
            // 3 4 5
            // 0 1 2

            foreach (GH_Path path in adjustedModules.Paths)
            {
                Brep currSegment = extendedSegments.Branch(path)[0];
                Rectangle3d currModule = adjustedModules.Branch(path)[0];

                // divide modules into pin influence areas
                List<Rectangle3d> pinAreas = new List<Rectangle3d>();

                for (int j = 0; j < pinsPerModuleY; j++)                   
                {
                    for (int i = 0; i < pinsPerModuleX; i++)
                    {
                        // divide domain 2 - MOVE to SHELLScommon? should exist already...
                        double DomainSizeX = currModule.X.T1 - currModule.X.T0;
                        double DomainSizeY = currModule.Y.T1 - currModule.Y.T0;

                        Interval currPinDomainX = new Interval(
                            currModule.X.T0 + i       * (DomainSizeX / pinsPerModuleX), 
                            currModule.X.T0 + (i + 1) * (DomainSizeX / pinsPerModuleX));

                        Interval currPinDomainY = new Interval(
                            currModule.Y.T0 + j * (DomainSizeY / pinsPerModuleY),
                            currModule.Y.T0 + (j + 1) * (DomainSizeY / pinsPerModuleY));

                        pinAreas.Add(new Rectangle3d(currModule.Plane, currPinDomainX, currPinDomainY));
                    }

                }

                // get pin heights and colors
                foreach (Rectangle3d pinArea in pinAreas)
                {
                    // get pinArea center
                    Point3d pinStart = pinArea.Center;

                    // project center onto extended segment
                    Point3d[] pinEnds = Intersection.ProjectPointsToBreps(
                        new Brep[] { currSegment }, 
                        new Point3d[] { pinStart },
                        currModule.Plane.ZAxis,
                        DocumentTolerance()
                        );

                    // values for color
                    double minHue = 0.33; // green
                    double maxHue = 0.16; //yellow
                    double pinColorSat = 1.00;
                    double pinColorLightness = 0.40;
                    double minPinLength = 0.00;
                    Color pinColor = new Color();

                    if (pinEnds.Length > 0) // intersection successful
                    {
                        Point3d pinEnd = pinEnds[0];
                        Line pinAxis = new Line(pinStart, pinEnd);
                        double pinLength = pinAxis.Length;
                        pinAxes.Add(pinAxis, path);
                        pinLengths.Add(pinLength, path);

                        // color pinAxes
                        
                        if (pinLength > maxPinLength) pinColor = Color.Red;
                        else
                        {
                            // interpolate color from pinLength (0-1)
                            double pinColorHue = minHue + (pinLength - minPinLength) / (maxPinLength - minPinLength) * (maxHue - minHue);
                            pinColor = new ColorHSL(pinColorHue, pinColorSat, pinColorLightness);
                        }
                        pinColors.Add(pinColor, path);
                    }
                    else // border cases?
                    {
                        Line pinAxis = new Line(pinStart, pinStart); //0-length line
                        pinAxes.Add(pinAxis, path);
                        pinLengths.Add(0, path);

                        pinColor = new ColorHSL(minHue, pinColorSat, pinColorLightness);
                        pinColors.Add(pinColor, path);
                    }
                }
            }

            DA.SetDataTree(0, segments);
            DA.SetDataTree(1, extendedSegments);
            DA.SetDataTree(2, adjustedModules);
            DA.SetDataTree(3, segmentBoxes);
            DA.SetDataTree(4, pinAxes);
            DA.SetDataTree(5, pinLengths);
            DA.SetDataTree(6, pinColors);


        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.simPinbed;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("3137bd7c-0f11-4688-b9dc-c57f003eb552"); }
        }
    }
}