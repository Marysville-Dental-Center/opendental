// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.UI {
	///<summary>Draws Test capture's histogram as grey pixels, so the window shows it with one image and the drawing can be tested: a white
	///background, one dark column per bin scaled to the tallest bin, and mid-grey lines at the window's ends.</summary>
	public static class HistogramPicture {
		public const byte Background=255;
		public const byte Bar=60;
		public const byte Marker=170;

		///<summary>width*height grey bytes, rows top to bottom. bins may be null (an empty picture). markerBins: bins to mark with a vertical
		///line (for example the window's ends), or null.</summary>
		public static byte[] Render(int[] bins,int width,int height,int[] markerBins=null) {
			if(width<=0 || height<=0) {
				throw new ArgumentException("The picture has no size.");
			}
			byte[] arrayPixels=new byte[width*height];
			for(int i=0;i<arrayPixels.Length;i++) {
				arrayPixels[i]=Background;
			}
			if(bins==null || bins.Length==0) {
				return arrayPixels;
			}
			int countBins=bins.Length;
			if(markerBins!=null) {
				foreach(int bin in markerBins) {
					if(bin<0 || bin>=countBins) {
						continue;
					}
					int x=(int)((long)bin*width/countBins);
					for(int y=0;y<height;y++) {
						arrayPixels[y*width+x]=Marker;
					}
				}
			}
			long max=0;
			foreach(int count in bins) {
				max=Math.Max(max,count);
			}
			if(max==0) {
				return arrayPixels;
			}
			for(int x=0;x<width;x++) {
				//the bins this column covers; the tallest of them is drawn
				int binFirst=(int)((long)x*countBins/width);
				int binLast=Math.Max(binFirst,(int)((long)(x+1)*countBins/width)-1);
				long countColumn=0;
				for(int bin=binFirst;bin<=binLast && bin<countBins;bin++) {
					countColumn=Math.Max(countColumn,bins[bin]);
				}
				if(countColumn==0) {
					continue;
				}
				int heightBar=Math.Max(1,(int)Math.Round((double)countColumn*height/max));
				for(int y=height-heightBar;y<height;y++) {
					arrayPixels[y*width+x]=Bar;
				}
			}
			return arrayPixels;
		}
	}
}
