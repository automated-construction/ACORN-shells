import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd

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