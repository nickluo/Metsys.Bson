using System;
using System.Linq;

namespace Metsys.Bson
{
    public static class GuidExtension
    {
        /// <summary>
        /// Reverse sequence between java style to .net style.
        /// </summary>
        /// <param name="rawGuid">The guid to be reversed.</param>
        /// <returns>Reversed guid</returns>
        public static Guid Reverse(this Guid rawGuid)
        {
            var bytes = rawGuid.ToByteArray();
            var reverse = bytes.Take(4).Reverse();
            reverse = reverse.Concat(bytes.Skip(4).Take(2).Reverse());
            reverse = reverse.Concat(bytes.Skip(6).Take(2).Reverse());
            reverse = reverse.Concat(bytes.Skip(8));
            return new Guid(reverse.ToArray());
        }
    }
}
