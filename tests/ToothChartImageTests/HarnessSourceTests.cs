// TEMPORARY (package TC-2): proves that the harness in Harness\LegacyCallers.cs is the callers' own chart code. Every HARNESS-BEGIN block must
// appear in its program file line for line (indentation aside). The test lives only while the program still has that code: TC-2's commit (1)
// moves the code into the painter and deletes this test with the blocks it checked; the call logs recorded from the harness stay.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ToothChartImageTests {
	[TestClass]
	public class HarnessSourceTests {
		private const string BEGIN="//HARNESS-BEGIN ";
		private const string END="//HARNESS-END";

		[TestMethod]
		public void Harness_EveryBlockIsTheProgramsCodeLineForLine() {
			string[] arrayHarness=ReadLines(Path.Combine(TestPaths.ProjectFolder,"Harness","LegacyCallers.cs"));
			List<string> listProblems=new List<string>();
			int countBlocks=0;
			int countLines=0;
			for(int i=0;i<arrayHarness.Length;i++) {
				if(!arrayHarness[i].StartsWith(BEGIN)) {
					continue;
				}
				string relative=arrayHarness[i].Substring(BEGIN.Length).Trim();
				List<string> listBlock=new List<string>();
				int j=i+1;
				for(;j<arrayHarness.Length && arrayHarness[j]!=END;j++) {
					listBlock.Add(arrayHarness[j]);
				}
				Assert.IsTrue(j<arrayHarness.Length,"A block from line "+(i+1)+" has no end.");
				Assert.IsTrue(listBlock.Count>=20,"Block at line "+(i+1)+" is too short to be a whole piece of chart code.");
				countBlocks++;
				countLines+=listBlock.Count;
				string[] arrayProgram=ReadLines(Path.Combine(TestPaths.RepoRoot,relative));
				if(IndexOfBlock(arrayProgram,listBlock)<0) {
					listProblems.Add("The block at line "+(i+1)+" ("+listBlock.Count+" lines) is not in "+relative);
				}
				i=j;
			}
			Assert.AreEqual(13,countBlocks,"Four from ControlChart, three from each of the other callers.");
			Assert.IsTrue(countLines>1000,"The blocks hold the callers' chart code: "+countLines+" lines.");
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>Lines with their indentation trimmed: the harness nests the code one level deeper in places.</summary>
		private static string[] ReadLines(string path) {
			return File.ReadAllText(path).Replace("\r\n","\n").TrimStart('﻿').Split('\n').Select(x => x.Trim()).ToArray();
		}

		private static int IndexOfBlock(string[] arrayLines,List<string> listBlock) {
			List<string> listBlockTrimmed=listBlock.Select(x => x.Trim()).ToList();
			for(int start=0;start+listBlockTrimmed.Count<=arrayLines.Length;start++) {
				bool isMatch=true;
				for(int k=0;k<listBlockTrimmed.Count;k++) {
					if(arrayLines[start+k]!=listBlockTrimmed[k]) {
						isMatch=false;
						break;
					}
				}
				if(isMatch) {
					return start;
				}
			}
			return -1;
		}
	}
}
