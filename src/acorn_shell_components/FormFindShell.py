from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_shell.generate import form_find

class FormFindShell(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "FormFindShell", "A:FormFindShell", """Use Kiwi3D to form-find the shell. Uses Kiwi3D v0.5.0""", "ACORN", "Shell")
        return instance
    
    def __init__(self):
        self.form_found_surface = None
        self.kiwi_errors = ""

    def get_ComponentGuid(self):
        return System.Guid("e8ae8a80-389a-417a-8a84-134f95f0da52")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "plan_surface", "P", "Flat surface to be form found.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "corners", "C", "Radius to bevel corners by.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "height", "H", "Target height of shell.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Boolean()
        self.SetUpParam(p, "run", "R", "Toggle to run analysis.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "form_found_surface", "S", "Form found shell.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_String()
        self.SetUpParam(p, "kiwi_errors", "E", "Errors from Kiwi3D.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        plan_surface = self.marshal.GetInput(DA, 0)
        corners = self.marshal.GetInput(DA, 1)
        height = self.marshal.GetInput(DA, 2)
        run = self.marshal.GetInput(DA, 3)
        
        if (run):
            found_surf, errs = form_find(plan_surface, corners, height, run)

            self.form_found_surface = found_surf
            self.kiwi_errors = errs
        
        if self.form_found_surface is not None:
            self.marshal.SetOutput(self.form_found_surface, DA, 0, True)
            self.marshal.SetOutput(self.kiwi_errors, DA, 1, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAALTSURBVEhL3ZPLS1RhGMbf75wzpVmYdrfSLiQKWUE3yLCMkiKUggkimkVkm9RFKpGtulIhkUKWRUTgHxAt27RrVdCiKAgNIrowRJHpzDiOvT3PMGeY25mZRase+OF8x/f+vZ/897ISf/OpGJucagezQL4Axdjk1FIQBqtBKcgVoBgbT50BCvpAJZgNMuVlUzARDV4DOr8DS0AZSHX0sjkFCo7MATSm8zqwLHHmd1e5bNaAokdGAzrTeFHinKlMm7OJc76xJuUDNOZF0pnnTKXacERvEmevsabpJoiCYbAYzAUMmMoAcG2qAKtmAq+xJsWsEwrZcNjpAf9HwZZzXwnYKc/5xhoPzi0YxI+ZwFZL9aEvyfSgo6FLjo732tq1wWiJLbpcJLRAZAt82IU7MnyKd5mmfuAH80Bloy13hvabmBt8ZtinkRuOTpx39GenrcGApV/aLL1cJdEmS17BZy1gAo6nHKQlYOUMToNR0NEgciywQsaT1Q+g+ouO/uqx9Xu7rd+OWPqpxdKx7UZPlEi40chIwr8GVIC0BAbwUj6ASdCDW63bbUt06h6qv4vqrzv6uw/Vd6D645Z+brX0Y5PR0Y1GX1aLrhKZhl8MPADsImtNmbETdIMg8LeUyvsXvY5Gb6H6C6i+G9WftPWrH9XvMzq2zejTGtH+cpnBuriXzgKrATcvbU3ZBW+ea3kIBGuNPLm918Qi11D9OUd/nLb17WFLR3DBXRUSbjYytcdIcIeRx3B8Bp8QeAS4VbyHrDV1k3CnD4IISvhztN7o1QaJtpXJZLMl4V2WPF8vcgXrcgA2+Cn1oA7UAq4pH1pWB66YhJnngzDb5spuNnIfcw7g2ybAgNwajoLz5lrSnrByz+Cp4iUNAc6UbWOx4kGx+vGHxGBzAO14f3h7cVhcweAUjfgm+HA4UwZeCBjUDVhUoHxiALbrts3AHOE/FZMU3Xa6RP4CiT/w3vG81vEAAAAASUVORK5CYII="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))