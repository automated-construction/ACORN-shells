import clr
from os import path
import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd

from System.Collections.Generic import List

KARAMBA_R5 = ["C:\Program Files\Rhino 5\Plug-ins\Karamba\Karamba.gha",
    "C:\Program Files\Rhino 5\Plug-ins\Karamba\KarambaCommon.dll"]

KARAMBA_R6 = ["C:\Program Files\Rhino 6\Plug-ins\Karamba\Karamba.gha",
    "C:\Program Files\Rhino 6\Plug-ins\Karamba\KarambaCommon.dll"]

KARAMBA_R7 = ["C:\Program Files\Rhino 7\Plug-ins\Karamba\Karamba.gha",
    "C:\Program Files\Rhino 7\Plug-ins\Karamba\KarambaCommon.dll"]

if (path.exists(KARAMBA_R5[0])):
    clr.AddReferenceToFileAndPath(KARAMBA_R5[0])
    clr.AddReferenceToFileAndPath(KARAMBA_R5[1])
elif (path.exists(KARAMBA_R6[0])):
    clr.AddReferenceToFileAndPath(KARAMBA_R6[0])
    clr.AddReferenceToFileAndPath(KARAMBA_R6[1])
elif (path.exists(KARAMBA_R7[0])):
    clr.AddReferenceToFileAndPath(KARAMBA_R7[0])
    clr.AddReferenceToFileAndPath(KARAMBA_R7[1])

import Karamba
import KarambaCommon

def build_shell_model(mesh, corners, thickness, e = 35000000, g_ip = 12920000, g_op = 12920000, density = 25,
    f_y = 25000, alpha_t = 0.00001):
    '''Build gravitational load Karamba3D model of the shell

    Parameters:
        mesh (Mesh): Meshed surface
        corners (List[Curve]): Support curves.
        thickness (float): Thickness of shell.

    Returns:
        k3d_model (Model): Karamba3D model.
        gh_model (Model): Karamba3d model wrapped for GH components.
    '''
    
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    logger = Karamba.Utilities.MessageLogger()
    k3d_kit = KarambaCommon.Toolkit()

    # Material
    k3d_mat = k3d_kit.Material.IsotropicMaterial('CONC', 'CONC', e, g_ip, g_op,
        density, f_y, alpha_t)
    
    # Cross section
    k3d_sec = k3d_kit.CroSec.ShellConst(thickness, 0.0, k3d_mat,
        'shellSect', 'shellSect', 'UK')
    
    # Create shell elements
    k3d_mesh = Karamba.GHopper.Geometry.MeshExtensions.Convert(mesh)
    k3d_nodes = clr.StrongBox[List[Karamba.Geometry.Point3]]()
    k3d_shell = k3d_kit.Part.MeshToShell(
        List[Karamba.Geometry.Mesh3]([k3d_mesh]),
        List[str](['shell']),
        List[Karamba.CrossSections.CroSec]([k3d_sec]),
        logger,
        k3d_nodes)
    k3d_nodes = k3d_nodes.Value

    # Fixed supports
    support_points = []
    for i in range(len(mesh.Vertices)):
        p = mesh.Vertices[i]
        for c in corners:
            test = c.ClosestPoint(p, tol)[0]
            if (test):
                support_points.append(p)
                break
    k3d_supports = [k3d_kit.Support.Support(
        Karamba.GHopper.Geometry.GeometryExtensions.Convert(p),
        List[bool]([True, True, True, True, True, True])) for p in support_points]
    
    # Gravitational load
    k3d_load = Karamba.Loads.GravityLoad(Karamba.Geometry.Vector3(0, 0, -1))
    
    # Assemble
    out_info = clr.StrongBox[str]()
    out_mass = clr.StrongBox[float]()
    out_cog = clr.StrongBox[Karamba.Geometry.Point3]()
    out_msg = clr.StrongBox[str]()
    out_run_warn = clr.StrongBox[bool]()
    k3d_model = k3d_kit.Model.AssembleModel(
        List[Karamba.Elements.BuilderElement](k3d_shell),
        List[Karamba.Supports.Support](k3d_supports),
        List[Karamba.Loads.Load]([k3d_load]),
        out_info, out_mass, out_cog, out_msg, out_run_warn)
    
    return [k3d_model, Karamba.GHopper.Models.GH_Model(k3d_model)]