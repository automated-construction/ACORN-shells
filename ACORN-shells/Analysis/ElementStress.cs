using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Karamba.Geometry;
using Karamba.GHopper.Models;
using Karamba.Results;
using Karamba.Models;
using Karamba.GHopper.Geometry;

namespace ACORN_shells
{
    /// <summary>
    /// This class is only used by Analysis > StressProbe, and should therefore be moved there
    /// </summary>
    public class ElementStress
    {
        public int Element { get; set; }
        public int Mesh { get; set; } // mesh to which element belongs - support for multiple meshes
        public int Face { get; set; } // face index in Mesh
        public Point3d princ_origin_top { get; set; }
        public Vector3d princ_vec1_top { get; set; }
        public Vector3d princ_vec2_top { get; set; }
        public double princ_val1_top { get; set; }
        public double princ_val2_top { get; set; }
        public Point3d princ_origin_bottom { get; set; }
        public Vector3d princ_vec1_bottom { get; set; }
        public Vector3d princ_vec2_bottom { get; set; }
        public double princ_val1_bottom { get; set; }
        public double princ_val2_bottom { get; set; }

       public ElementStress()
        {
        }



        public static List<ElementStress> GetElementStresses(Model k3dModelAnalysis, out List<Mesh> rhMeshes)
        {
            // extract original shell mesh from k3dModel
            List<IMesh> k3dMeshes = new List<IMesh>();
            k3dModelAnalysis.Disassemble(out _, out _, out k3dMeshes, out _, out _, out _, out _, out _, out _, out _, out _);
            // for segmented shell analysis, needs to cope with multipe meshes
            rhMeshes = new List<Mesh>();
            foreach (IMesh k3dMesh in k3dMeshes) rhMeshes.Add(k3dMesh.Convert());

            // get first and second principal stress values for all elements, top and bottom layers
            var superimp_factors = new feb.VectReal { 1 }; // according to https://discourse.mcneel.com/t/shell-principal-stresses-in-karamba-api/120629
            // get stresses for top layer
            PrincipalStressDirs.solve(k3dModelAnalysis, 0, 1, superimp_factors, 
                out List<Point3> princ_origins_top, 
                out List<Vector3> princ_vec1s_top, out List<Vector3> princ_vec2s_top,
                out List<double> princ_val1s_top, out List<double> princ_val2s_top);
            // get stresses for bottom layer
            PrincipalStressDirs.solve(k3dModelAnalysis, 0, -1, superimp_factors,
                out List<Point3> princ_origins_bottom,
                out List<Vector3> princ_vec1s_bottom, out List<Vector3> princ_vec2s_bottom,
                out List<double> princ_val1s_bottom, out List<double> princ_val2s_bottom);

            //List<List<double>> PSlists = new List<List<double>> { bottomPS1s, bottomPS2s, topPS1s, topPS2s };
            //List<double> allPSs = PSlists.SelectMany(e => e).ToList();

            // create list of stressValue objects, pairing stress values and element indexes,  to sort "asynchronously" as in GH sort component
            List<ElementStress> elementStresses = new List<ElementStress>();

            // determine which mesh it belongs to through list partition, and which face in that mesh
            int meshIndex = 0;
            int faceIndex = 0;
            for (int elementIndex = 0; elementIndex < princ_vec1s_top.Count; elementIndex++)
            {
                // element count is for ALL meshes, face count is for belonging mesh

                // creates instance of StressValue 
                elementStresses.Add(new ElementStress
                {
                    Element = elementIndex,
                    Mesh = meshIndex,
                    Face = faceIndex,
                    princ_origin_top = princ_origins_top[elementIndex].Convert(),
                    princ_vec1_top = princ_vec1s_top[elementIndex].Convert(),
                    princ_vec2_top = princ_vec2s_top[elementIndex].Convert(),
                    princ_val1_top = princ_val1s_top[elementIndex],
                    princ_val2_top = princ_val2s_top[elementIndex],
                    princ_origin_bottom = princ_origins_bottom[elementIndex].Convert(),
                    princ_vec1_bottom = princ_vec1s_bottom[elementIndex].Convert(),
                    princ_vec2_bottom = princ_vec2s_bottom[elementIndex].Convert(),
                    princ_val1_bottom = princ_val1s_bottom[elementIndex],
                    princ_val2_bottom = princ_val2s_bottom[elementIndex]               
                });

                // manage counts
                if (faceIndex < rhMeshes[meshIndex].Faces.Count - 1)
                    faceIndex++;
                else // reached the end of iterating all mesh's faces so next mesh
                {
                    meshIndex++;
                    faceIndex = 0;
                }
            }

            return elementStresses;

        }

        public double CalculateMaximumCompression() // returns minimum (most negative) stress value, being compression in Karamba
        {
            List<double> fourStresses = new List<double> { this.princ_val1_top, this.princ_val1_bottom, this.princ_val2_top, this.princ_val2_bottom };
            List<double> sortedStresses = fourStresses.OrderBy(e => e).ToList();
            return sortedStresses[0];
        }

        public double CalculateMaximumTension() // returns maximum (most positive) stress value, being tension in Karamba
        {
            List<double> fourStresses = new List<double> { this.princ_val1_top, this.princ_val1_bottom, this.princ_val2_top, this.princ_val2_bottom };
            List<double> sortedStresses = fourStresses.OrderByDescending(e => e).ToList();
            return sortedStresses[0];
        }

        /// <summary>
        /// Creates meshes with the elements top % stress values
        /// Support and outputs multiple meshes
        /// </summary>
        /// <param name="origMeshes"></param>
        /// <param name="SVs"></param>
        /// <returns></returns>
        public static List<Mesh> MakeExtremeMeshes(List<Mesh> origMeshes, List<ElementStress> SVs)
        {
            List<Mesh> extMeshes = new List<Mesh>();

            // copy vertices from original mesh(es) to extreme mesh
            foreach (Mesh rhMesh in origMeshes)
            {
                Mesh meshTens = new Mesh();
                meshTens.Vertices.AddVertices(rhMesh.Vertices);
                extMeshes.Add(meshTens);
            }

            // copy top valued element faces from original mesh to extreme mesh
            foreach (ElementStress sv in SVs)
                //meshComp.Faces.AddFace(rhMesh.Faces[sv.Element]);
                extMeshes[sv.Mesh].Faces.AddFace(origMeshes[sv.Mesh].Faces[sv.Face]);

            // finish off
            foreach (Mesh extMesh in extMeshes)
            {
                extMesh.Normals.ComputeNormals();
                extMesh.Compact();
            }

            return extMeshes;
        }



    }
}
