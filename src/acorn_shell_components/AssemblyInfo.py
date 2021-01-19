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
        return System.Guid("2fd98af9-c2c6-43bb-8773-c06dcf0e22dd")