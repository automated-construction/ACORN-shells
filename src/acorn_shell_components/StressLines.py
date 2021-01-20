from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_shell.generate import stress_lines

class StressLines(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "StressLines", "ACORN_StressLines", """Generate stress lines and cable profiles from Karamba model.""", "ACORN", "Shell")
        return instance

    def get_ComponentGuid(self):
        return System.Guid("d1a6f0a5-e0af-47aa-a837-14d21afa85a0")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_GenericObject()
        self.SetUpParam(p, "k3d_model", "k3d_model", "Analysed Karamba3D model.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "surface", "surface", "Form found shell brep.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "corners", "corners", "Corner curves of shell.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "edges", "edges", "Edge curves of shell.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "keystone_width", "keystone_width", "Width of the keystone.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "cornerstone_width", "cornerstone_width", "Width of the cornerstone.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "length_param_1", "length_param_1", "Distance between stress lines 1.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "length_param_2", "length_param_2", "Distance between stress lines 2.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "stress_lines_1", "stress_lines_1", "Stress lines related to tension.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "stress_lines_2", "stress_lines_2", "Stress lines related to compression.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "cable_profiles_1", "cable_profiles_1", "Stress lines related to tension.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "cable_profiles_2", "cable_profiles_2", "Stress lines related to compression.")
        self.Params.Output.Add(p)


    def SolveInstance(self, DA):
        k3d_model = self.marshal.GetInput(DA, 0)
        surface = self.marshal.GetInput(DA, 1)
        corners = self.marshal.GetInput(DA, 2)
        edges = self.marshal.GetInput(DA, 3)
        keystone_width = self.marshal.GetInput(DA, 4)
        cornerstone_width = self.marshal.GetInput(DA, 5)
        length_param_1 = self.marshal.GetInput(DA, 6)
        length_param_2 = self.marshal.GetInput(DA, 7)

        stress_lines_1, stress_lines_2, cable_profiles_1, cable_profiles_2 = stress_lines(k3d_model, surface, corners, edges, keystone_width,
            cornerstone_width, length_param_1, length_param_2)
        
        self.marshal.SetOutput(stress_lines_1, DA, 0, True)
        self.marshal.SetOutput(stress_lines_2, DA, 1, True)
        self.marshal.SetOutput(cable_profiles_1, DA, 2, True)
        self.marshal.SetOutput(cable_profiles_2, DA, 3, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAANxSURBVEhLxZXbj45XFId/xtA6DOPQcRiqDp0ISgiCpiJuhFRENVQPqQp1aDUSlYhDRGj/AxcuJU3MTeOGpCWOdSXiFNKEC4m73jSpViuY8Dzv973ozHw+d3byZGbvb5322mutN69j9YTGyr/PVg/wrKHYPV+lrL/XX98mF79Jrs5KhrN9o3KaRXAffoAB0AsKWbi1JlnFti90DqrL6onxm98lTzYmR9m/Bf1gGXTAE/gFRkITspeVrcofn5mM47wP1LxN44pkOlH9o9IXyX7OdNIERvkX6OQ0jBmVtK5N9iH/r/I4vLskmcdvytd00ufzZJMKW0jLVBxyNhiaYSH8CTo5CK3yQTJ/c3JdHZz9MSN5j/OaTnzE5g3JWRXWJz+xHwH9YRB8BKbrMcyGFhgyNmkjTVfU4VbtnA2D8g27rN5Lk/eJpoNbPJ6cTONsoOfgTU6AtzgAGvKBB5Ce+dWb/81+DHgLq6zb1W9TckGF1ckO9mVEVtD3oIOfwTQVFbQ8maI8b3GP/TvgjWtWVuNXyVYVvk7Os+dNn5XibtCBqdBBESnv0F6VP8m+roMGIp+tAop32HvlsgdOgQ52QuEAmb3KktYHcyt9UzdF4R2GqWQ1sR0PRuTfR+AjW5KtFMKeqvGOTxL+zQSwMN6E2ouctlQd/MdWpSFwCIz+OIynV3aUxrnxds6UGw0WReex8v9FuU1XmUczRSpOggeggw9pisVWmTKfVt7F270N9k3dsRF6YYPK1UdWeR9o/Ddo4/yMv69LjlR/N+8aL2ZVvdVA5Kc18HGyi71z5jbogArOBNJSjAjalmYufh8K9kr9RVSTMfDIFLxb6dg5oHGbiN7LWBtKB2wWsDd6y/jVFsZ/7XT9laCDS9AGo7jGOWV4q8PsX90BNf2ZipbnxISyDqOmGNk6uAo6aCF1C72lslST3wpT9PLS5OEmolSO6x85MnrnP74KB5ass0ljTXT7LmWFfmh3hHPuN6H7hfHig0NqjrG1NC07a1qla6CTbeAU9ayZQLZz24fqkbYbnPkN6XaSNlI5l0jRTaxa86bGSC0765pPRdHVX4JGTIdVM3gxfYHx39E1CLvYMdGlF5wbzn1rWSGNlNf14+EjeuZkdS4ZpecG4Bg3laK+n9pum610YgSdr1k68ffSeLnUMxgdv2A8eQp2d9H0J32T5QAAAABJRU5ErkJggg=="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))