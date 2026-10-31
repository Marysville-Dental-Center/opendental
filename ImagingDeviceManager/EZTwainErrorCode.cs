// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
namespace ImagingDeviceManager {
	///<summary>Error codes returned by EZTwain.LastErrorCode(). The numeric values are kept stable because callers compare
	///LastErrorCode() against them (user cancelled = 19, no pages = 38, paper jam = 76, and so on).
	///Only codes that this implementation can produce, or that callers check, are defined.</summary>
	public enum EZTwainErrorCode {
		///<summary>0. No error.</summary>
		EZTEC_NONE=0,
		///<summary>2. Reading a capability from the data source failed.</summary>
		EZTEC_CAP_GET=2,
		///<summary>3. Setting a capability on the data source failed.</summary>
		EZTEC_CAP_SET=3,
		///<summary>4. A call to the TWAIN Data Source Manager failed.</summary>
		EZTEC_DSM_FAILURE=4,
		///<summary>5. A call to the data source (scanner driver) failed.</summary>
		EZTEC_DS_FAILURE=5,
		///<summary>6. An image transfer failed.</summary>
		EZTEC_XFER_FAILURE=6,
		///<summary>8. The TWAIN Data Source Manager could not be opened.</summary>
		EZTEC_OPEN_DSM=8,
		///<summary>9. The default data source could not be opened.</summary>
		EZTEC_OPEN_DEFAULT_DS=9,
		///<summary>10. The operation requires an open data source that is not acquiring.</summary>
		EZTEC_NOT_STATE_4=10,
		///<summary>17. The window handle passed in is not a valid window.</summary>
		EZTEC_INVALID_HWND=17,
		///<summary>18. The hidden parent window used for TWAIN could not be created.</summary>
		EZTEC_PROXY_WINDOW=18,
		///<summary>19. The user cancelled the scan.</summary>
		EZTEC_USER_CANCEL=19,
		///<summary>22. JPEG support is missing. Kept for callers; this implementation never returns it.</summary>
		EZTEC_JPEG_DLL=22,
		///<summary>24. Neither TWAINDSM.dll nor twain_32.dll could be loaded.</summary>
		EZTEC_LOAD_DSM=24,
		///<summary>25. No data source with the requested name is installed.</summary>
		EZTEC_NO_SUCH_DS=25,
		///<summary>26. The data source could not be opened.</summary>
		EZTEC_OPEN_DS=26,
		///<summary>27. The data source could not be enabled (started).</summary>
		EZTEC_ENABLE_FAILED=27,
		///<summary>28. The memory transfer returned data in a layout that cannot be converted to a bitmap.</summary>
		EZTEC_BAD_MEMXFER=28,
		///<summary>31. The image handle is not a valid image.</summary>
		EZTEC_BAD_DIB=31,
		///<summary>32. The file name is empty or invalid.</summary>
		EZTEC_BAD_FILENAME=32,
		///<summary>35. Out of memory.</summary>
		EZTEC_MEMORY=35,
		///<summary>38. The scan finished without producing any pages.</summary>
		EZTEC_0_PAGES=38,
		///<summary>39. The output file type is not supported for writing.</summary>
		EZTEC_UNK_WRITE_FF=39,
		///<summary>42. Writing the PDF file failed.</summary>
		EZTEC_PDF_WRITE_ERR=42,
		///<summary>43. PDF support is missing. Kept for callers; this implementation never returns it.</summary>
		EZTEC_NO_PDF=43,
		///<summary>47. Writing the output file failed.</summary>
		EZTEC_FILE_WRITE=47,
		///<summary>48. The operation requires an open data source.</summary>
		EZTEC_NO_DS_OPEN=48,
		///<summary>57. An argument was not valid.</summary>
		EZTEC_INVALID_PARAM=57,
		///<summary>58. Internal error, including an acquire started while another acquire was still running.</summary>
		EZTEC_INTERNAL=58,
		///<summary>76. The scanner reported a paper jam.</summary>
		EZTEC_DEVICE_PAPERJAM=76,
		///<summary>77. The scanner reported a double feed.</summary>
		EZTEC_DEVICE_DOUBLEFEED=77,
	}
}
