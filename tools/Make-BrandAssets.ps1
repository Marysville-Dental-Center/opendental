<#
.SYNOPSIS
Draws the Open PMS - Dental brand assets from the logo in branding\logo.svg.

.DESCRIPTION
The logo is a white molar on a teal rounded square (branding\logo.svg is the vector master). This script draws, with WPF and no other tool:
  branding\app.ico            program and window icon: 16, 20, 24, 32, 40, 48, 64, 128 (32-bit) and 256 (PNG) pixels
  branding\logo-256.png       the logo for documents and the installer
  branding\splash.png         the 500x300 start-up splash
  branding\installer-small.bmp, installer-small-2x.bmp   the installer's small wizard image (55 and 110 pixels)
  branding\installer-wizard.bmp, installer-wizard-2x.bmp the installer's welcome-page image (164x314 and 328x628)
The same input always gives the same files. Keep the shapes here in step with branding\logo.svg.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Make-BrandAssets.ps1
#>
param([string]$OutDir='')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $OutDir) { $OutDir=Join-Path $root 'branding' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$code=@'
using System;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class BrandArt {
	public static readonly Color Teal=Color.FromRgb(0x0F,0x76,0x6E);
	public static readonly Color Ink=Color.FromRgb(0x0F,0x17,0x2A);
	public static readonly Color Muted=Color.FromRgb(0x47,0x55,0x69);
	public static readonly Color Faint=Color.FromRgb(0x64,0x74,0x8B);
	public const string Molar="M22 30 C22 14 38 12 50 20 C62 12 78 14 78 30 C78 44 74 52 72 62 C70 74 68 88 61 88 C55 88 55 72 50 66 C45 72 45 88 39 88 C32 88 30 74 28 62 C26 52 22 44 22 30 Z";

	///<summary>Draws the logo (a 100x100 design) at the given place and size.</summary>
	public static void DrawLogo(DrawingContext dc,double x,double y,double size) {
		double s=size/100.0;
		dc.PushTransform(new TranslateTransform(x,y));
		dc.PushTransform(new ScaleTransform(s,s));
		dc.DrawRoundedRectangle(new SolidColorBrush(Teal),null,new Rect(0,0,100,100),22,22);
		Geometry molar=Geometry.Parse(Molar).Clone();
		TransformGroup tg=new TransformGroup();
		tg.Children.Add(new ScaleTransform(0.82,0.82));
		tg.Children.Add(new TranslateTransform(9,8));
		molar.Transform=tg;
		dc.DrawGeometry(Brushes.White,null,molar);
		dc.Pop();
		dc.Pop();
	}

	static RenderTargetBitmap Render(int width,int height,Action<DrawingContext> draw) {
		DrawingVisual visual=new DrawingVisual();
		RenderOptions.SetEdgeMode(visual,EdgeMode.Unspecified);
		using(DrawingContext dc=visual.RenderOpen()) {
			draw(dc);
		}
		RenderTargetBitmap rtb=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
		rtb.Render(visual);
		rtb.Freeze();
		return rtb;
	}

	public static BitmapSource Logo(int size) {
		return Render(size,size,dc => DrawLogo(dc,0,0,size));
	}

	static FormattedText Text(string text,double size,FontWeight weight,Color color) {
		return new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
			new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,weight,FontStretches.Normal),size,new SolidColorBrush(color),1.0);
	}

	public static BitmapSource Splash() {
		return Render(500,300,dc => {
			dc.DrawRectangle(Brushes.White,null,new Rect(0,0,500,300));
			dc.DrawRectangle(new SolidColorBrush(Teal),null,new Rect(0,0,500,6));
			DrawLogo(dc,40,62,112);
			dc.DrawText(Text("Open PMS - Dental",30,FontWeights.SemiBold,Ink),new Point(176,78));
			dc.DrawText(Text("Dental practice management",15,FontWeights.Normal,Muted),new Point(178,124));
			dc.DrawRectangle(new SolidColorBrush(Teal),null,new Rect(178,154,56,3));
			dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xE2,0xE8,0xF0)),null,new Rect(40,214,420,1));
			dc.DrawText(Text("Based on Open Dental. \u00A9 Jordan Sparks, DMD, Open Dental Software, Inc. and contributors.",10.5,FontWeights.Normal,Faint),new Point(40,224));
			dc.DrawText(Text("Free software under the GNU GPL, version 2 or later. It comes with no warranty.",10.5,FontWeights.Normal,Faint),new Point(40,242));
			dc.DrawText(Text("Not affiliated with or endorsed by Open Dental Software, Inc.",10.5,FontWeights.Normal,Faint),new Point(40,260));
		});
	}

	public static BitmapSource InstallerSmall(int size) {
		return Render(size,size,dc => {
			dc.DrawRectangle(Brushes.White,null,new Rect(0,0,size,size));
			double logo=size*0.86;
			DrawLogo(dc,(size-logo)/2,(size-logo)/2,logo);
		});
	}

	public static BitmapSource InstallerWizard(int scale) {
		int w=164*scale, h=314*scale;
		return Render(w,h,dc => {
			dc.DrawRectangle(new SolidColorBrush(Teal),null,new Rect(0,0,w,h));
			double logo=96*scale;
			dc.PushTransform(new TranslateTransform((w-logo)/2,48*scale));
			dc.DrawRoundedRectangle(Brushes.White,null,new Rect(-4*scale,-4*scale,logo+8*scale,logo+8*scale),24*scale,24*scale);
			dc.Pop();
			DrawLogo(dc,(w-logo)/2,48*scale,logo);
			FormattedText t1=Text("Open PMS",20*scale,FontWeights.SemiBold,Colors.White);
			FormattedText t2=Text("Dental",20*scale,FontWeights.Normal,Colors.White);
			dc.DrawText(t1,new Point((w-t1.Width)/2,172*scale));
			dc.DrawText(t2,new Point((w-t2.Width)/2,200*scale));
		});
	}

	public static void SavePng(BitmapSource src,string path) {
		PngBitmapEncoder enc=new PngBitmapEncoder();
		enc.Frames.Add(BitmapFrame.Create(src));
		using(FileStream fs=File.Create(path)) { enc.Save(fs); }
	}

	///<summary>A 24-bit BMP (no alpha), the format Inno Setup's wizard images take on every Windows version.</summary>
	public static void SaveBmp24(BitmapSource src,string path) {
		FormatConvertedBitmap bgr=new FormatConvertedBitmap(src,PixelFormats.Bgr24,null,0);
		BmpBitmapEncoder enc=new BmpBitmapEncoder();
		enc.Frames.Add(BitmapFrame.Create(bgr));
		using(FileStream fs=File.Create(path)) { enc.Save(fs); }
	}

	///<summary>An .ico with 32-bit DIB images up to 128 pixels and a PNG image at 256, which System.Drawing.Icon and Windows both read.</summary>
	public static void SaveIco(int[] sizes,string path) {
		byte[][] images=new byte[sizes.Length][];
		for(int i=0;i<sizes.Length;i++) {
			BitmapSource bmp=Logo(sizes[i]);
			if(sizes[i]>=256) {
				PngBitmapEncoder enc=new PngBitmapEncoder();
				enc.Frames.Add(BitmapFrame.Create(bmp));
				using(MemoryStream ms=new MemoryStream()) { enc.Save(ms); images[i]=ms.ToArray(); }
				continue;
			}
			images[i]=Dib32(bmp);
		}
		using(FileStream fs=File.Create(path))
		using(BinaryWriter bw=new BinaryWriter(fs)) {
			bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
			int offset=6+16*sizes.Length;
			for(int i=0;i<sizes.Length;i++) {
				bw.Write((byte)(sizes[i]>=256 ? 0 : sizes[i]));
				bw.Write((byte)(sizes[i]>=256 ? 0 : sizes[i]));
				bw.Write((byte)0); bw.Write((byte)0);
				bw.Write((short)1); bw.Write((short)32);
				bw.Write(images[i].Length); bw.Write(offset);
				offset+=images[i].Length;
			}
			foreach(byte[] img in images) { bw.Write(img); }
		}
	}

	static byte[] Dib32(BitmapSource bmp) {
		int n=bmp.PixelWidth;
		//Straight (not premultiplied) BGRA, as icons store it.
		FormatConvertedBitmap bgra=new FormatConvertedBitmap(bmp,PixelFormats.Bgra32,null,0);
		byte[] px=new byte[n*n*4];
		bgra.CopyPixels(px,n*4,0);
		int maskStride=((n+31)/32)*4;
		using(MemoryStream ms=new MemoryStream())
		using(BinaryWriter bw=new BinaryWriter(ms)) {
			bw.Write(40); bw.Write(n); bw.Write(n*2); bw.Write((short)1); bw.Write((short)32);
			bw.Write(0); bw.Write(n*n*4+maskStride*n); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
			for(int row=n-1;row>=0;row--) { bw.Write(px,row*n*4,n*4); }
			for(int row=n-1;row>=0;row--) {
				byte[] mask=new byte[maskStride];
				for(int col=0;col<n;col++) {
					if(px[(row*n+col)*4+3]==0) { mask[col/8]|=(byte)(0x80>>(col%8)); }
				}
				bw.Write(mask);
			}
			return ms.ToArray();
		}
	}
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies PresentationCore,PresentationFramework,WindowsBase,System.Xaml

[BrandArt]::SaveIco([int[]]@(16,20,24,32,40,48,64,128,256),(Join-Path $OutDir 'app.ico'))
[BrandArt]::SavePng([BrandArt]::Logo(256),(Join-Path $OutDir 'logo-256.png'))
[BrandArt]::SavePng([BrandArt]::Splash(),(Join-Path $OutDir 'splash.png'))
[BrandArt]::SaveBmp24([BrandArt]::InstallerSmall(55),(Join-Path $OutDir 'installer-small.bmp'))
[BrandArt]::SaveBmp24([BrandArt]::InstallerSmall(110),(Join-Path $OutDir 'installer-small-2x.bmp'))
[BrandArt]::SaveBmp24([BrandArt]::InstallerWizard(1),(Join-Path $OutDir 'installer-wizard.bmp'))
[BrandArt]::SaveBmp24([BrandArt]::InstallerWizard(2),(Join-Path $OutDir 'installer-wizard-2x.bmp'))
Get-ChildItem $OutDir | Where-Object { $_.Name -ne 'logo.svg' -and $_.Name -ne 'README.md' } | ForEach-Object { '{0,-26} {1,8} bytes' -f $_.Name,$_.Length }
