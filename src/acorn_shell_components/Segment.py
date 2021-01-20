from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_shell.generate import segment

class Segment(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "Segment", "ACORN_Segment", """Segment shell using stress lines.""", "ACORN", "Shell")
        return instance
    
    def __init__(self):
        self.form_found_surface = None
        self.kiwi_errors = ""

    def get_ComponentGuid(self):
        return System.Guid("29762d30-517f-49f2-a0e2-9d382cdbf473")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "surface", "surface", "Form found shell brep.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "stress_lines_1", "stress_lines_1", "Stress lines related to tension.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "stress_lines_2", "stress_lines_2", "Stress lines related to compression.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "segments", "segments", "Segmented shell pieces.")
        self.Params.Output.Add(p)


    def SolveInstance(self, DA):
        surface = self.marshal.GetInput(DA, 0)
        stress_lines_1 = self.marshal.GetInput(DA, 1)
        stress_lines_2 = self.marshal.GetInput(DA, 2)

        result = segment(surface, stress_lines_1, stress_lines_2)
        
        if result is not None:
            self.marshal.SetOutput(result, DA, 0, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAStSURBVEhLvZV7bFN1FMd/97a3va8+ttv1sa3bumH36MYeRXHIzMIjwiZxy8JUBupE2GADNgwTNjAumviHBthEHQ4TITH+JcEXI4T5QJGhgqgjhEgUE50oc8tq6Vra9Xh+t9mobiaTEE/y/eOe3/l9zrm/c+7vklmYjLKhElAMddwu06ASdS6y0jCXuSrcQTbjM01yW4wnAknVzSE9xiJ2rP5gStTs1QS4DLIG10yxkFs3o9ZO7tW7yWBWJX+p66cc6PkjD9o+zgBMdl3nJCswxhAL/e8molKEXGbUXSVc6R31qPCXr+VC99VsaDySAoZCxqd1kDKM49UdsWR2lIKi/dKiphmHskhu9kFlvmYos5IPm4rZaPupzCn47iE37DiTDuZ5bFCfSRoxnsJYxLvFfMbnWKz92V6u6dYmk1L0W1FGFO0jka138qWu5fx3toVcpOlwGryGlbsq9UFsbrBjIEOFt37oBFMJG+Td7FO4h1ZLJ0rUZZKW9Pu5kereBChqEMDkZcPGEuY3OZ/Zg+sWFLHLBcxoxTMW6LmWr8JfxWNpftcJhiJ2BKGBVa/b6NEE9S7yBMbT6ug00eoUKZ8ZqNqfEG05Z4NNX1lhw4AFyjolkIuYy7iuJjDjtLxU2mQcmYTv+z0HuvBYnEs4vz6bHBI9TECXRmro2Ys55BXZQ7opnDOTAkwQaj5thU1fWmHjGQs0nFYgq5YL4Ju1YYxEE2h0CsmWChhf59ksFb7nFzc8csgOlgWagOQhb+iySAcm+cZQzPjvbpXHZTw6LokU4TfSaivXhOr7lCl4XZ8ZsCfjtDfIZmkCaib9HPJsQZ3orztgj1pKNSFs8pCYy/Tj0YTTKnW/rn4zKdw+6IQdF1JgXrMYkvJIL59LxmhSLC6cWqEdX7xbgvntQlSaS44iMzGGjhlL9CQDezFh9rIR51LOV7rF4H+gSwFrmTbi3SBFdl6Mwbd9mwxrjyXRKgMmLzNcfdAEC3aKYF+iCRmLmQk+hwDvInXIFGLom2bFSr5v6XfC81cy4bkfXNB5OR28DVIIE9/Ydj5ZhW89b4ct2FAcy2BGNTe2/vNEWPtZAjx20gwPHTWA6CEh/EKcyJt2b5n5bPJ2dbcSmYQ/fSkNKl40g1zIDFfuNavwxpNJsHCXNCHlk/BdbUJ4Er7mIyMs6hZw8phzyKJjPM3oTG8vrBfHJuEdF1Oh7i0L4Fz/qNyjCRSu50P4lkFs8vvYmwvLe+Qp+Kp+A+Q1chG8Yl5A1oxXCadPJctsZVrfxuMOWHlAgfJdxomsGt11sYAZRugJHNEuOj0Y61CfPeRG8jKNr+RJ/cSifTwoZcyfeCHW4rpeJf7DGNpoHMeo2qg4Iehvz1RCHpkWJ3hIlDJU1r+YgwZuH6TT4oCtX9th81mrurkRv9B1kw39xKT6Hj4hQ+1xCWqOiVD1Aa/6KCOGmtnUBPHwpi+S1I0U/vinCfAowlfjmVNfPHzFe/rZJ2iNg9PKqS8eTiunvpo+YQpe8Q43+wQ34QqsO5WoboyH08qpLwbXQcURDu47rJ1VAjs2z08D4zVjQ2fy5RI/ZcRQMxv9idAAWsWtiO6ljP/LCPkLiugScN+cr0oAAAAASUVORK5CYII="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))