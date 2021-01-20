from ghpythonlib import components as ghcomp
import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd
from System import IO
import os
import math
import acorn_shell
from acorn_shell.karamba import principal_stress_line

def segment(surface, stress_lines_1, stress_lines_2):
    '''Segment shell using stress lines.

    Parameters:
        surface (Brep): Form found shell brep.
        stress_lines_1 (List[Curve]): Stress lines related to tension to segment with.
        stress_lines_2 (List[Curve]): Stress lines related to compression to segment with.

    Returns:
        segments (List[Surface]): Segmented shell pieces.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    # Intersect with each stress line sets and retrieve polyline
    segment_lines = []
    points_1 = {}
    points_2 = {}
    for i in range(len(stress_lines_1)):
        points_1[i] = []
        s_1 = stress_lines_1[i]
        for j in range(len(stress_lines_2)):
            if (not j in points_2):
                points_2[j] = []
            s_2 = stress_lines_2[j]
            intersections = rg.Intersect.Intersection.CurveCurve(s_1, s_2, tol, tol)
            points_1[i].extend([x.ParameterA for x in intersections])
            points_2[j].extend([x.ParameterB for x in intersections])
    
    intersection_points = []

    for k, v in points_1.items():
        v.sort()
        points = [stress_lines_1[k].PointAt(t) for t in v]
        intersection_points.extend(points)
        segment_lines.append(rg.PolylineCurve(points))
    
    for k, v in points_2.items():
        v.sort()
        points = [stress_lines_2[k].PointAt(t) for t in v]
        intersection_points.extend(points)
        segment_lines.append(rg.PolylineCurve(points))
    
    # Make keystone
    area_mass_prop = rg.AreaMassProperties.Compute(surface)
    centroid = area_mass_prop.Centroid

    wires = surface.GetWireframe(-1)
    num_edges = wires.Count / 2

    point_cloud = rg.PointCloud(intersection_points)

    keystone_points = []
    for i in range(num_edges):
        idx = point_cloud.ClosestPoint(centroid)
        p = point_cloud[idx].Location
        keystone_points.append(p)
        point_cloud.RemoveAt(idx)
        # Need to remove the point closest to this point since all points are doubled
        idx = point_cloud.ClosestPoint(p)
        point_cloud.RemoveAt(idx)
    
    # Order keystone points
    translated_keystone_points = [p - centroid for p in keystone_points]
    sorting_criteria = []
    for p in translated_keystone_points:
        mag = math.sqrt(p.X**2 + p.Y**2)
        if p.Y > 0:
            sorting_criteria.append(math.acos(p.X / mag))
        else:
            sorting_criteria.append(2 * math.pi - math.acos(p.X / mag))
    zip_points = zip(sorting_criteria, keystone_points)
    zip_points = sorted(zip_points)
    keystone_points = [p for _, p in zip_points]

    keystone_points.append(keystone_points[0])
    keystone = rg.PolylineCurve(keystone_points)
    segment_lines.append(keystone)

    # Segment
    segments = surface.Split(segment_lines, rg.Vector3d.ZAxis, True, tol)

    return segments


def stress_lines(k3d_model, surface, corners, edges, keystone_width, cornerstone_width,
    length_param_1, length_param_2):
    '''Generate stress lines and cable profiles from Karamba model.

    TODO: Allow custom material from material testing data?

    Parameters:
        k3d_model (Model): Analysed Karamba3D model.
        surface (Brep): Form found shell brep.
        corners (List[Curve]): Corner curves of shell.
        edges (List[Curve]): Edge curves of shell.
        keystone_width (float): Width of the keystone.
        cornerstone_width (float): Width of the cornerstone.
        length_param_1 (float): Distance between stress lines 1.
        length_param_2 (float): Distance between stress lines 2.

    Returns:
        stress_lines_1 (List[Curve]): Stress lines related to tension.
        stress_lines_2 (List[Curve]): Stress lines related to compression.
        cable_profiles_1 (List[Curve]): Cable profiles related to tension.
        cable_profiles_2 (List[Curve]): Cable profiles related to compression.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    # Find centroid of shell
    area_mass_prop = rg.AreaMassProperties.Compute(surface)
    centroid = area_mass_prop.Centroid

    # Calculate points to analyse stress lines at
    edge_midpoints = [e.PointAtNormalizedLength(0.5) for e in edges]
    corner_midpoints = [c.PointAtNormalizedLength(0.5) for c in corners]
    wires = surface.GetWireframe(-1)
    wires_points = rg.PointCloud([w.PointAtNormalizedLength(0.5) for w in wires])
    edge_midpoints = [wires_points[wires_points.ClosestPoint(e)].Location for e in edge_midpoints]
    corner_midpoints = [wires_points[wires_points.ClosestPoint(c)].Location for c in corner_midpoints]

    # Use shortest path to get generating lines
    surface_surf = surface.Surfaces[0]
    centroid_uv = surface_surf.ClosestPoint(centroid)
    centroid_uv = rg.Point2d(centroid_uv[1], centroid_uv[2])

    edge_gen_lines = []
    keystone_points = []
    for p in edge_midpoints:
        p_uv = surface_surf.ClosestPoint(p)
        p_uv = rg.Point2d(p_uv[1], p_uv[2])
        line = surface_surf.ShortPath(centroid_uv, p_uv, tol)
        line = line.Trim(rg.CurveEnd.Start, keystone_width / 2 * math.sqrt(2))
        keystone_points.append(line.PointAtNormalizedLength(0))
        edge_gen_lines.append(line)

    # Get keystone polylinecurve to trim corner generating lines
    keystone_points.append(keystone_points[0])
    keystone_poly = rg.PolylineCurve(keystone_points)
    keystone_poly = rg.Curve.ProjectToBrep(keystone_poly, surface, rg.Vector3d.ZAxis, tol)[0]
    
    corner_gen_lines = []
    for p in corner_midpoints:
        p_uv = surface_surf.ClosestPoint(p)
        p_uv = rg.Point2d(p_uv[1], p_uv[2])
        line = surface_surf.ShortPath(centroid_uv, p_uv, tol)
        line = line.Trim(rg.CurveEnd.End, cornerstone_width)
        intersect = rg.Intersect.Intersection.CurveCurve(line, keystone_poly, tol, tol)[0]
        param_trim = intersect.ParameterA
        line = line.Trim(param_trim, 0)
        corner_gen_lines.append(line)

    # Find source points
    stress_line_sources_1 = []
    cable_line_sources_1 = []
    for l in corner_gen_lines:
        length = l.GetLength()
        divs = math.ceil(length / length_param_1) # Want ceil to use as upper bound
        stress_params = [i / divs for i in range(1, int(divs) + 1)]
        cable_params = [(i + 0.5) / divs for i in range(int(divs))]
        stress_line_sources_1.extend([l.PointAtNormalizedLength(t) for t in stress_params])
        cable_line_sources_1.extend([l.PointAtNormalizedLength(t) for t in cable_params])
    
    stress_line_sources_2 = []
    cable_line_sources_2 = []
    for l in edge_gen_lines:
        length = l.GetLength()
        divs = math.ceil(length / length_param_2) # Want ceil to use as upper bound
        stress_params = [i / divs for i in range(int(divs))]
        cable_params = [(i + 0.5) / divs for i in range(int(divs))]
        stress_line_sources_2.extend([l.PointAtNormalizedLength(t) for t in stress_params])
        cable_line_sources_2.extend([l.PointAtNormalizedLength(t) for t in cable_params])
   
    # Get stress lines
    stress_lines_1, _ = principal_stress_line(k3d_model, stress_line_sources_1)
    _, stress_lines_2,= principal_stress_line(k3d_model, stress_line_sources_2)
    cable_profiles_1, _ = principal_stress_line(k3d_model, cable_line_sources_1)
    _, cable_profiles_2,= principal_stress_line(k3d_model, cable_line_sources_2)

    # Add line from centroid to edge to tension stress lines set
    for p in edge_midpoints:
        p_uv = surface_surf.ClosestPoint(p)
        p_uv = rg.Point2d(p_uv[1], p_uv[2])
        line = surface_surf.ShortPath(centroid_uv, p_uv, tol)
        stress_lines_1.append(line)
    
    # Add edges to compression stress line set
    for e in edges:
        p_mid = e.PointAtNormalizedLength(0.5)
        idx = wires_points.ClosestPoint(p_mid)
        stress_lines_2.append(wires[idx])
    
    return [stress_lines_1, stress_lines_2, cable_profiles_1, cable_profiles_2]

def form_find(plan_surface, corners, height, run):
    '''Use Kiwi3D to form-find the shell.
    Uses Kiwi3D v0.5.0

    TODO: Allow custom material from material testing data?

    Parameters:
        plan_surface (Brep): Flat surface to be form found.
        corners (List[Curve]): Curves of plan corners.
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
        corner_radius (float): Radius to bevel corners by.

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