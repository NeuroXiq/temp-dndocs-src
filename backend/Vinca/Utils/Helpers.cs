using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vinca.Utils
{
    public class Helpers
    {
        /// <summary>
        /// Check if two byte arrays have same bytes
        /// throws if null byte arrays
        /// </summary>
        /// <param name="b1"></param>
        /// <param name="b2"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool BytesInArrayEquals(byte[] b1, byte[] b2)
        {
            if (b1 == null) throw new ArgumentNullException("b1");
            if (b2 == null) throw new ArgumentNullException("b2");

            if (b1.Length != b2.Length) return false;

            for (int i = 0; i < b1.Length; i++) if (b1[i] != b2[i]) { return false; }

            return true;
        }
    }
}
