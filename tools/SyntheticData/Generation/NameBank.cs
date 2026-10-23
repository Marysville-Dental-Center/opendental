// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	///<summary>Invented names, streets, towns and companies. Surnames are compounds of nature words and place endings
	///("Brindlewick", "Quorvale"); given names are built from syllables and filtered against a list of common real given names and
	///surnames. Phone numbers use the 555-0100 to 555-0199 block reserved for fiction; e-mail addresses use example.com, example.net
	///and example.org, which are reserved for documentation.</summary>
	public static class NameBank {
		private static readonly string[] _arraySurnameFirst={
			"Amber","Brindle","Cinder","Dapple","Ember","Fallow","Gorse","Hollow","Ivory","Juniper","Kestrel","Lantern","Marrow",
			"Nettle","Oaken","Pebble","Quor","Rook","Sorrel","Thistle","Umber","Vellum","Wren","Yarrow","Zephyr","Bracken","Copper",
			"Driftly","Fennel","Gable","Heron","Inkle","Lumen","Mossy","Nimble","Orchid","Pewter","Quartz","Russet","Sable","Tansy",
		};
		private static readonly string[] _arraySurnameSecond={
			"wick","vale","grave","mere","stead","combe","ridge","holt","gate","marsh","shaw","fell","hithe","tarn","wold","crest",
		};
		private static readonly string[] _arrayGivenStart={
			"Ael","Bryn","Caer","Dorv","Elv","Fen","Gael","Hask","Iov","Jory","Kev","Lir","Mev","Nyl","Orv","Pell","Quen","Rhy","Syl",
			"Tav","Ulv","Vey","Wyl","Xan","Yev","Zel","Aust","Bram","Corv","Dael","Esk","Fyn","Ilv","Kael","Lorv","Myr","Ress","Tir",
		};
		private static readonly string[] _arrayGivenEnd={
			"a","an","en","ia","is","o","ric","wyn","eth","ar","ine","on","ys","ette","ira","ius","elle","ard","amo","ix",
		};
		private static readonly string[] _arrayStreetTypes={"Lane","Way","Court","Row","Path","Terrace","Close","Loop","Crossing","Walk"};
		private static readonly string[] _arrayTownEnds={"ford","haven","bury","field","port","ton","moor"};
		private static readonly string[] _arrayCompanyKinds={
			"Logistics","Textiles","Ceramics","Robotics","Orchards","Freight","Publishing","Foundry","Mills","Analytics","Cooperative","Labs",
		};
		private static readonly string[] _arrayCarrierEnds={"Dental Benefits","Dental Mutual","Smile Assurance","Dental Plan Trust","Oral Health Plan"};

		///<summary>Common real given names and surnames that the generator must never produce. Used as a filter and by the tests.</summary>
		public static readonly HashSet<string> SetCommonRealNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"James","Mary","John","Patricia","Robert","Jennifer","Michael","Linda","William","Elizabeth","David","Barbara","Richard","Susan",
			"Joseph","Jessica","Thomas","Sarah","Charles","Karen","Christopher","Nancy","Daniel","Lisa","Matthew","Betty","Anthony","Margaret",
			"Mark","Sandra","Donald","Ashley","Steven","Kimberly","Paul","Emily","Andrew","Donna","Joshua","Michelle","Kenneth","Dorothy",
			"Kevin","Carol","Brian","Amanda","George","Melissa","Edward","Deborah","Ronald","Stephanie","Timothy","Rebecca","Jason","Sharon",
			"Jeffrey","Laura","Ryan","Cynthia","Jacob","Kathleen","Gary","Amy","Nicholas","Shirley","Eric","Angela","Jonathan","Helen",
			"Stephen","Anna","Larry","Brenda","Justin","Pamela","Scott","Nicole","Brandon","Emma","Benjamin","Samantha","Samuel","Katherine",
			"Gregory","Christine","Frank","Debra","Alexander","Rachel","Raymond","Catherine","Patrick","Carolyn","Jack","Janet","Dennis","Ruth",
			"Jerry","Maria","Tyler","Heather","Aaron","Diane","Jose","Virginia","Adam","Julie","Henry","Joyce","Nathan","Victoria","Douglas",
			"Olivia","Zachary","Kelly","Peter","Christina","Kyle","Lauren","Walter","Joan","Ethan","Evelyn","Jeremy","Judith","Harold","Megan",
			"Keith","Cheryl","Christian","Andrea","Roger","Hannah","Noah","Martha","Gerald","Jacqueline","Carl","Frances","Terry","Gloria",
			"Sean","Ann","Austin","Teresa","Arthur","Kathryn","Lawrence","Sara","Jesse","Janice","Dylan","Jean","Bryan","Alice","Joe","Madison",
			"Rosa","Loren","Elena","Ava","Mia","Liam","Ella","Owen","Ian","Eva","Nina","Leon","Iris","Ivan","Otto","Hugo","Tara","Vera",
			"Smith","Johnson","Williams","Brown","Jones","Garcia","Miller","Davis","Rodriguez","Martinez","Hernandez","Lopez","Gonzalez",
			"Wilson","Anderson","Taylor","Moore","Jackson","Martin","Lee","Perez","Thompson","White","Harris","Sanchez","Clark","Ramirez",
			"Lewis","Robinson","Walker","Young","Allen","King","Wright","Scott","Torres","Nguyen","Hill","Flores","Green","Adams","Nelson",
			"Baker","Hall","Rivera","Campbell","Mitchell","Carter","Roberts","Ashford","Marshfield","Ridgeway","Holt","Wicks",
		};

		public static string Surname(Rng rng) {
			for(int attempt=0;;attempt++) {
				string name=rng.Pick(_arraySurnameFirst)+rng.Pick(_arraySurnameSecond);
				if(!SetCommonRealNames.Contains(name) || attempt>20) {
					return name;
				}
			}
		}

		public static string GivenName(Rng rng) {
			for(int attempt=0;;attempt++) {
				string name=rng.Pick(_arrayGivenStart)+rng.Pick(_arrayGivenEnd);
				if(!SetCommonRealNames.Contains(name) || attempt>20) {
					return name;
				}
			}
		}

		public static string MiddleInitial(Rng rng) {
			return rng.Chance(0.6) ? ((char)('A'+rng.Next(26))).ToString() : "";
		}

		public static string Street(Rng rng) {
			return rng.Between(10,9899)+" "+rng.Pick(_arraySurnameFirst)+" "+rng.Pick(_arrayStreetTypes);
		}

		public static string Town(Rng rng) {
			return rng.Pick(_arraySurnameFirst)+rng.Pick(_arrayTownEnds);
		}

		///<summary>(xxx)555-01nn: the 555-0100 to 555-0199 block is reserved for fictional use.</summary>
		public static string Phone(Rng rng) {
			string[] arrayAreaCodes={"206","253","360","425","509"};
			return "("+rng.Pick(arrayAreaCodes)+")555-01"+rng.Next(100).ToString("00");
		}

		public static string Email(string givenName,string surname,long uniqueNum) {
			string[] arrayDomains={"example.com","example.net","example.org"};
			return (givenName+"."+surname).ToLowerInvariant()+uniqueNum+"@"+arrayDomains[uniqueNum%3];
		}

		public static string Company(Rng rng) {
			return rng.Pick(_arraySurnameFirst)+rng.Pick(_arraySurnameSecond)+" "+rng.Pick(_arrayCompanyKinds);
		}

		public static string CarrierName(Rng rng) {
			return rng.Pick(_arraySurnameFirst)+rng.Pick(_arraySurnameSecond)+" "+rng.Pick(_arrayCarrierEnds);
		}

		public static string Zip(Rng rng) {
			return "98"+rng.Between(100,999);
		}
	}
}
