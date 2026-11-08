// The one helper of the legacy UnitTests/CoreTypesT.cs that the ported Middle Tier tests use (MiddleTier_GetStringLong), copied unchanged.
// CodeBase's MiscUtils.CreateRandomAlphaNumericString concatenates strings and is far too slow for the million characters of that test.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;

namespace MiddleTierMockTests {
	public class CoreTypesT {
		public static string CreateRandomAlphaNumericString(int length){
			StringBuilder result=new StringBuilder(length);
			Random rand=new Random();
			string randChrs="abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			for(int i=0;i<length;i++){
				result.Append(randChrs[rand.Next(0,randChrs.Length-1)]);
			}
			return result.ToString();
		}
	}
}
