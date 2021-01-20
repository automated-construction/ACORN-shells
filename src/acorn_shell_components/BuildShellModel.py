from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_shell.karamba import build_shell_model

class BuildShellModel(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "BuildShellModel", "ACORN_BuildShellModel", """Build gravitational load Karamba3D model of the shell. Force units in kN and length units in document units.""", "ACORN", "Shell")
        return instance

    def get_ComponentGuid(self):
        return System.Guid("610cc4be-617e-4586-a607-d5a9b6bf7165")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Mesh()
        self.SetUpParam(p, "mesh", "mesh", "Meshed surface.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "corners", "corners", "Support curves.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "thickness", "thickness", "Thickness of shell.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_GenericObject()
        self.SetUpParam(p, "k3d_model", "k3d_model", "Karamba3D model.")
        self.Params.Output.Add(p)

    def SolveInstance(self, DA):
        mesh = self.marshal.GetInput(DA, 0)
        corners = self.marshal.GetInput(DA, 1)
        thickness = self.marshal.GetInput(DA, 2)

        _, result = build_shell_model(mesh, corners, thickness)
        
        if result is not None:
            self.marshal.SetOutput(result, DA, 0, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAANBSURBVEhL7ZNtSJVnGMd/gtpmc0PafGEim30oZm0ksQz2QVjFhH0QIzjNtppioZPytTeW06FLp9WsIYGWzlVSY4s+RImJp55zfI7nHPWgRbbUSitcWztqL4ygc3bdz3kc+3CIzdWn9oeH+3nu+7n+1//6X9fN/3iWiDbXp4aQwBK67Pgmem/X8SgnleLA3szZ7BAIfn3VOx+9S5uviT/k8T5uxOv7jgffWOjgTUuM8c9sEPsyS5uz6Ly9h2khnD5byMS9A/zua2a6o4jRk3no7YX0xkfxgRnyxGoChxv9YYviST9bgPXHXG49bOCuUmyq9h7NYfxgJp5f9tI9vBtt+Cu0oUrcn6WylzUXw/1BkySuSVg+n/SSVdSLsglR+FBZ4TuM17aNiRs1/HpnH781rONSVwlWayn6sRz6b+3Bfq0GzVqC3pqNfiSHLiLikk1WhbfCq1czNF7HfV8LD/wtTGpbmfDuZ9JQ3MzU3Xq8H6dgL/uQ82KVbXQ3NqX6ciVa03rczZ/Sc34rNpVIzrQL2/GkLyEfv/+vSpJ+rmLSN2NDC1MD5Uz+lMfo+wupzUyhUwKd8u3sKsWpSK5Xc6GjmB7pgUts7HPtwq32VfLxWrSWLKy8EJdg8htI6i3De66E6wfWUs/ceanzoylyfc6g2KOPSKAiEGt6TgnpiY14+spwGMlEeackO5GL62oVjoIVNLGgNNLk/RtiVgYuT+iLKYc2oF+sYKBNiMa+RlMJRJlN1Dq+z8ZxeAMuscs+k/iaVHQyH1f8q1gMjiCNDiEiIa54JY1XKhmSINdINY7Bcrpbs+gXC7XG9Tg9ZfQowrFauqXhbrXKpdOrMmgnISPR5AqOyHAWnN6CW5qlSLrlsY+L+lZp4s40HDJFAcXVgUarCuoteNLeZodJ8cQ7EMCc6MRT+Ya3tktfYm/8BOdgBbr0wd6QiVtsMpKoRkpltlBYbkb+A/IZhEUlfStkym9FZKiWsZRq7PstkqSO3qIVHGRxbpQZMSssPL3ZsMmYbWWLmnOZd+dL4aw2//kXqoNgbhiLZPz6VIKbtehyIc8w55U3zOOngzBJ8sMmBjKS+cLc+m+qgyL2vdfMt2dA/pwD/gSvyIZvBHoBRQAAAABJRU5ErkJggg=="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))