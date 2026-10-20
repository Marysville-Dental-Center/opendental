// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Text;

namespace SyntheticData.Infrastructure {
	///<summary>Deterministic pseudo-random numbers (SplitMix64). System.Random is not used because its sequence is an implementation detail
	///of the framework. The same seed and stream name always give the same numbers on every machine and framework version.</summary>
	public sealed class Rng {
		private ulong _state;

		public Rng(ulong seed) {
			_state=seed;
		}

		///<summary>An independent stream for one part of the generator, so that adding draws in one part never shifts another.</summary>
		public static Rng Derive(long seed,string stream) {
			return new Rng(Mix(unchecked((ulong)seed)^Fnv1a64(stream)));
		}

		///<summary>A child stream named after a sub-task of this stream. Consumes one draw from this stream.</summary>
		public Rng Fork(string name) {
			return new Rng(Mix(NextULong()^Fnv1a64(name)));
		}

		public ulong NextULong() {
			unchecked {
				_state+=0x9E3779B97F4A7C15UL;
				ulong z=_state;
				z=(z^(z>>30))*0xBF58476D1CE4E5B9UL;
				z=(z^(z>>27))*0x94D049BB133111EBUL;
				return z^(z>>31);
			}
		}

		///<summary>Uniform integer in [0,maxExclusive).</summary>
		public int Next(int maxExclusive) {
			if(maxExclusive<=0) {
				throw new ArgumentOutOfRangeException(nameof(maxExclusive));
			}
			return (int)(NextULong()%(ulong)maxExclusive);
		}

		///<summary>Uniform integer in [minInclusive,maxInclusive].</summary>
		public int Between(int minInclusive,int maxInclusive) {
			if(maxInclusive<minInclusive) {
				throw new ArgumentOutOfRangeException(nameof(maxInclusive));
			}
			return minInclusive+Next(maxInclusive-minInclusive+1);
		}

		///<summary>Uniform double in [0,1).</summary>
		public double NextDouble() {
			return (NextULong()>>11)*(1.0/(1UL<<53));
		}

		public bool Chance(double probability) {
			return NextDouble()<probability;
		}

		public T Pick<T>(IReadOnlyList<T> list) {
			if(list.Count==0) {
				throw new ArgumentException("Cannot pick from an empty list.");
			}
			return list[Next(list.Count)];
		}

		///<summary>Picks an item with the given relative weights.</summary>
		public T PickWeighted<T>(IReadOnlyList<(T item,double weight)> listWeighted) {
			double total=0;
			for(int i=0;i<listWeighted.Count;i++) {
				total+=listWeighted[i].weight;
			}
			double roll=NextDouble()*total;
			for(int i=0;i<listWeighted.Count;i++) {
				roll-=listWeighted[i].weight;
				if(roll<0) {
					return listWeighted[i].item;
				}
			}
			return listWeighted[listWeighted.Count-1].item;
		}

		///<summary>Money amount in whole cents between min and max.</summary>
		public decimal Money(decimal min,decimal max) {
			long cents=(long)Math.Round(min*100)+(long)(NextDouble()*(double)((max-min)*100));
			return cents/100m;
		}

		///<summary>Upper-case hexadecimal string of the given number of random bytes.</summary>
		public string Hex(int byteCount) {
			StringBuilder stringBuilder=new StringBuilder(byteCount*2);
			for(int i=0;i<byteCount;i++) {
				stringBuilder.Append(((byte)NextULong()).ToString("X2"));
			}
			return stringBuilder.ToString();
		}

		public byte[] Bytes(int count) {
			byte[] bytes=new byte[count];
			for(int i=0;i<count;i++) {
				bytes[i]=(byte)NextULong();
			}
			return bytes;
		}

		///<summary>Shuffles the list in place (Fisher-Yates).</summary>
		public void Shuffle<T>(IList<T> list) {
			for(int i=list.Count-1;i>0;i--) {
				int j=Next(i+1);
				T temp=list[i];
				list[i]=list[j];
				list[j]=temp;
			}
		}

		public static ulong Fnv1a64(string text) {
			unchecked {
				ulong hash=14695981039346656037UL;
				foreach(char c in text??"") {
					hash^=c;
					hash*=1099511628211UL;
				}
				return hash;
			}
		}

		private static ulong Mix(ulong value) {
			unchecked {
				value=(value^(value>>33))*0xFF51AFD7ED558CCDUL;
				value=(value^(value>>33))*0xC4CEB9FE1A85EC53UL;
				return value^(value>>33);
			}
		}
	}
}
