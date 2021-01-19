from ghpythonlib import components as ghcomp
import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd
from System import IO
import os

def form_find(plan_surface, corners, height, run):
    '''Use Kiwi3D to form-find the shell.
    Uses Kiwi3D v0.5.0

    Parameters:
        plan_surface (Brep): Flat surface to be form found.
        corners (Curve): Curves of plan corners.
        height (float): Target height of shell.
        run (bool): Toggle to run analysis.

    Returns:
        form_found_surface (Brep): Form found brep.
        kiwi_errors (string): Errors from Kiwi3D.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    # Kiwi3D parameters
    MAT_CONC = 5
    SURF_DEGREE = 3
    SURF_SUBDIV = 10
    ANAL_OUTPUT = [1, 2, 3]
    thickness = height * 0.1

    tmp_directory = os.path.join(IO.Path.GetTempPath(), IO.Path.GetRandomFileName())
    if (os.path.exists(tmp_directory)):
        shutil.rmtree(tmp_directory)
    os.mkdir(tmp_directory)

    # Define model
    kiwi_material = ghcomp.Kiwi3d.MaterialDefaults(MAT_CONC)[0]
    kiwi_refinement = ghcomp.Kiwi3d.SurfaceRefinement(SURF_DEGREE,
        SURF_DEGREE, SURF_SUBDIV, SURF_SUBDIV)
    kiwi_shell = ghcomp.Kiwi3d.ShellElement(plan_surface, kiwi_material,
        thickness, kiwi_refinement, None, False)
    
    # Pinned support at corners
    # Divide the curves into points since the support curve seems to not work
    # as expected
    kiwi_supports = []
    support_points = [c.DivideByCount(100, True) for c in corners]
    for i in range(len(support_points)):
        kiwi_supports.extend([ghcomp.Kiwi3d.SupportPoint(corners[i].PointAt(t),
            True, True, True, False, False) for t in support_points[i]])
    # Uniform load pushing upwards
    kiwi_loads = ghcomp.Kiwi3d.SurfaceLoad(plan_surface, '1', rg.Vector3d.ZAxis,
        100, None, None, 1)

    # Run Kiwi analysis
    kiwi_a_option = ghcomp.Kiwi3d.LinearAnalysis('FormFinding', ANAL_OUTPUT)
    kiwi_model = ghcomp.Kiwi3d.AnalysisModel(kiwi_a_option, kiwi_shell,
        kiwi_supports, kiwi_loads)
    kiwi_result = ghcomp.Kiwi3d.IGASolver(kiwi_model, tmp_directory, run)
    kiwi_errors = kiwi_result[1]
    kiwi_result = kiwi_result[0]
    
    # Scale deformed shell to target height
    form_found_surface = ghcomp.Kiwi3d.DeformedModel(kiwi_result)[1]
    if (form_found_surface != None):
        bounds = form_found_surface.GetBoundingBox(rg.Plane.WorldXY)
        scale = height/(bounds.Max.Z - bounds.Min.Z)
        form_found_surface.Transform(
            rg.Transform.Scale(rg.Plane(bounds.Min, rg.Vector3d.ZAxis), 1, 1, scale))

    return [form_found_surface, kiwi_errors]

def make_2d_plan(outline, corner_radius):
    '''Creates a 2D shell plan on the XY plane.

    Parameters:
        outline (Curve): Outline curve. Should be a PolyLine.
        corner_radius (float): radius to bevel corner by.

    Returns:
        plan (Curve): Outline curve of generated plan.
        corners (List[Curve]): Curves of plan corners.
        edges (List[Curve]): Curves of plan edges.
    '''
    corners = []
    edges = []

    # Project onto XY plane
    outline = rg.Curve.ProjectToPlane(outline, rg.Plane.WorldXY)

    # Explode to lines
    exploded_outline = outline.DuplicateSegments()

    # Find centroid
    area_mass_prop = rg.AreaMassProperties.Compute(outline)
    centroid = area_mass_prop.Centroid

    # Trim edges to create plan edges
    edges = [l.Trim(rg.CurveEnd.Both, corner_radius) for l in exploded_outline]

    # Create corners
    for i in range(len(exploded_outline)):
        corner_point = exploded_outline[i].PointAtStart
        arc_start = exploded_outline[i].PointAtLength(corner_radius)
        arc_end = exploded_outline[i - 1].PointAtLength(
            exploded_outline[i - 1].GetLength() - corner_radius)
        to_centroid = rg.Vector3d(centroid - corner_point)
        to_centroid.Unitize()
        arc_interior = corner_point + to_centroid * corner_radius
        arc = rg.Arc(arc_start, arc_interior, arc_end)
        corners.append(rg.ArcCurve(arc))

    # Join edges and corners
    plan = rg.Curve.JoinCurves([a for b in zip(corners, edges) for a in b])
    
    return [plan, corners, edges]