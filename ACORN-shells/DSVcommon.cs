using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSVcommon
{
    public class DesignVector
    {
        public int ID { get; set; }
        public List<double> DesignMap { get; set; }
        public List<double> ObjValues { get; set; }
        public static List<string> DesignMapLabels { get; set; }
        public static List<string> ObjValuesLabels { get; set; }

        public DesignVector() { } // basic constructor

        public DesignVector(List<double> designMap) // constructor for Reference Vector: no ID or Objective Values
        {
            this.DesignMap = designMap;
        }
        public DesignVector(int id, List<double> designMap, List<double> objValues) // constructor for DV with Design Map and Objective Values
        {
            this.ID = id;
            this.DesignMap = designMap;
            this.ObjValues = objValues;
        }

        /// <summary>
        /// Returns euclidean distance between two DesignVectors
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static double GetVectorDistance (DesignVector v1, DesignVector v2)
        {
            double squaredDistance = 0;
            // iterate each dimension in the Design Map for calculating vector component squared distances
            for (int i = 0; i < v1.DesignMap.Count; i++)
                squaredDistance += Math.Pow (v1.DesignMap[i] - v2.DesignMap[i], 2);
            return Math.Sqrt(squaredDistance);
        }

        /// <summary>
        /// Finds DV in a list of DVs that is closest to a reference DV.
        /// </summary>
        /// <param name="designSpace"></param>
        /// <param name="refVector"></param>
        /// <param name="closestDistance"></param>
        /// <returns></returns>
        public static DesignVector FindClosestVector (List<DesignVector> designSpace, DesignVector refVector, out double closestDistance)
        {
            DesignVector closestVector = designSpace[0];
            closestDistance = GetVectorDistance(closestVector, refVector);

            foreach (DesignVector currVector in designSpace) // already did 0...
            {
                // find closest distance - use sort?
                double currDistance = GetVectorDistance(currVector, refVector);
                if (currDistance < closestDistance)
                {
                    closestDistance = currDistance;
                    closestVector = currVector;
                }
            }
            return closestVector;
        }

    }
}
