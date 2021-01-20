from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_shell.generate import make_2d_plan

class MakeShellPlan(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "MakeShellPlan", "A:MakeShellPlan", """Creates a 2D shell plan on the XY plane.""", "ACORN", "Shell")
        return instance
    
    def get_ComponentGuid(self):
        return System.Guid("34d234ec-d7dc-44fa-b5f2-07f6f2f95ed9")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "outline", "O", "Outline curve. Should be a PolyLine.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "corner_radius", "R", "Radius to bevel corners by.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "plan", "P", "Outline curve of generated plan.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "corners", "C", "Curves of plan corners.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "edges", "E", "Curves of plan edges.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        outline = self.marshal.GetInput(DA, 0)
        corner_radius = self.marshal.GetInput(DA, 1)
        
        plan, corners, edges = make_2d_plan(outline, corner_radius)

        if plan is not None:
            self.marshal.SetOutput(plan, DA, 0, True)
            self.marshal.SetOutput(corners, DA, 1, True)
            self.marshal.SetOutput(edges, DA, 2, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAKESURBVEhLvZRNSBRxGMYfd5tx1XVFTLMvqkNd9lJ0KTqUWSDUpYNBdSqEirAPsqIutWtmXxB0qIPRyVt4KOxkVHTwEkHQpe6pWRjl7rKoOP+eZ5yRzXXX2VZ64Qez/533feb/fuF/WwVZ4aHnZbWDZMzjMKkmyyaiQKPGMz5/I82kxvuvbFNKxrz4phLI7gF286yRREjZIhJQWsajgOndh9lbUWRuRPHmBNDK8ypikX8WkqNy3nydAuYdzMwrmPddcO43It1di09ngKMNQC3fkVCIyOQXuCn0Qp0v4OO8hfncC/NoM1I3qzFyMYQL24GVfPcQKbkpbFfg9eKMPIbp34lMMoJfNpD2SpbbFEuKWK7Ay+L8fApTb8Px4kvgO9lA6ojSVdDmBAaWZvAaTCwMJwz8pt95UoJAfzA+dMBcrsIQ/baSdSRgivqCMXkPJmkhRb84UQ2Kfr1srsgPg/OgAekDQDt9m4hauKC5u0iDNniczneCMdSCmbNh9NF3LSk49Tqc30WxEGaH2+BMnGOQRHFGT8EkbHyl/0aiQfSH8C9T7uZ3kTqjy8LzbhsTd2uQebENU1/aOd2XGPRqPj2VyO4AdjEGh33xOujQ3UXkB1HbqTPi3HhtJytwOxHBx2QYU0/WIzXcytvxy32BZ3FkOwCWD2uINnDeLZQidxcR9bNQ260i2qirdcYf8SPA6SsWBnJvl9wCw204zXc0cMcIS5lfC19EwyL0rJupM7jB3S+rJ+oWiW/aC+zX7fjitJddTbWyUHBt6EBBxcI/dW2dcw256ztGtPTUPeNe/JLWRjGTOHvBvZ3SobQosOrXScoWyDXdTCK59Qu0WUsxBVpYP54BfwCWaamtq2FlowAAAABJRU5ErkJggg=="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))

import GhPython
import System

class AssemblyInfo(GhPython.Assemblies.PythonAssemblyInfo):
    def get_AssemblyName(self):
        return "AcornShell"
    
    def get_AssemblyDescription(self):
        return """ACORN segmented shell generation and analysis."""

    def get_AssemblyVersion(self):
        return "0.1"

    def get_AuthorName(self):
        return "Eduardo Costa and Mishael Nuh"
    
    def get_Id(self):
        return System.Guid("a913e143-1f4e-4148-b168-56ed60b4ec82")