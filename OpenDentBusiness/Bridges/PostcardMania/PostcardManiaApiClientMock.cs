// PostcardMania (PCM Integrations) DirectMail API v3: an in-memory fake of the vendor for tests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace OpenDentBusiness.PostcardMania {
	///<summary>Behaves like the vendor, in memory: designs, orders with references (409 on a repeated extRefNbr), pricing at a fixed price per card,
	///cancel for pending orders. Set the Func hooks to make a call fail. Never touches the network.</summary>
	public class PostcardManiaApiClientMock:IPostcardManiaApiClient {
		public decimal PricePerCard=0.65m;
		public decimal MoneyOnAccount=100m;
		public List<PcmDesign> ListDesigns=new List<PcmDesign>();
		public List<PcmOrder> ListOrders=new List<PcmOrder>();
		///<summary>Every order request that reached PlacePostcardOrder, including ones that then failed.</summary>
		public List<PcmPostcardOrderRequest> ListPlaceRequests=new List<PcmPostcardOrderRequest>();
		///<summary>Every extRefNbr looked up through GetOrders.</summary>
		public List<string> ListOrderLookups=new List<string>();
		///<summary>Return an exception to throw instead of placing the order, or null to place it. The order is still recorded first when
		///IsPlacedBeforeFailure says so (a timeout after the vendor stored it).</summary>
		public Func<PcmPostcardOrderRequest,Exception> FuncPlaceFailure;
		public bool IsPlacedBeforeFailure;
		///<summary>Return true to reject these credentials.</summary>
		public Func<PostcardManiaAccount,bool> FuncRejectAccount;
		///<summary>Addresses for which this returns true are reported invalid by VerifyAddresses.</summary>
		public Func<PcmAddress,bool> FuncIsAddressInvalid;
		private long _orderIDNext=1000;
		private long _designIDNext=500;

		public PcmConnectionResult TestConnection(PostcardManiaAccount account) {
			CheckAccount(account);
			return new PcmConnectionResult {
				Environment=account.IsSandbox ? PostcardManiaProgram.EnvironmentSandbox : PostcardManiaProgram.EnvironmentProduction,
				MoneyOnAccount=MoneyOnAccount,
			};
		}

		public PcmPagedResult<PcmDesign> GetDesigns(PostcardManiaAccount account,string productType,int page,int perPage) {
			CheckAccount(account);
			return Page(ListDesigns,page,perPage);
		}

		public PcmDesignEditorResponse GetDesignEditUrl(PostcardManiaAccount account,long designID,bool duplicate) {
			CheckAccount(account);
			PcmDesign design=ListDesigns.FirstOrDefault(x => x.DesignID==designID);
			if(design==null) {
				throw new PostcardManiaException(PostcardManiaErrorKind.NotFound,"Design not found.",HttpStatusCode.NotFound);
			}
			if(design.IsUpload) {
				throw new PostcardManiaException(PostcardManiaErrorKind.Validation,"Uploaded designs can't be edited.",HttpStatusCode.BadRequest);
			}
			long designIDEdit=designID;
			if(duplicate) {
				designIDEdit=AddDesign(design.FriendlyName+" (copy)",design.Size?.Key).DesignID;
			}
			return new PcmDesignEditorResponse { DesignID=designIDEdit,Url="https://portal.example.test/editor/"+designIDEdit+"?token=mock" };
		}

		public PcmDesignEditorResponse CreateDesign(PostcardManiaAccount account,string name,string size) {
			CheckAccount(account);
			PcmDesign design=AddDesign(name,size);
			return new PcmDesignEditorResponse { DesignID=design.DesignID,Url="https://portal.example.test/editor/"+design.DesignID+"?token=mock" };
		}

		public PcmAddressVerificationResponse VerifyAddresses(PostcardManiaAccount account,List<PcmAddress> listAddresses) {
			CheckAccount(account);
			PcmAddressVerificationResponse response=new PcmAddressVerificationResponse();
			foreach(PcmAddress address in listAddresses) {
				if(FuncIsAddressInvalid!=null && FuncIsAddressInvalid(address)) {
					response.Results.Invalid.Add(address);
				}
				else {
					response.Results.Valid.Add(address);
				}
			}
			response.ValidCount=response.Results.Valid.Count;
			response.InvalidCount=response.Results.Invalid.Count;
			return response;
		}

		public PcmPricingResponse EstimatePostcardOrder(PostcardManiaAccount account,PcmPostcardOrderRequest request) {
			CheckAccount(account);
			int count=request.Recipients.Count;
			return new PcmPricingResponse {
				EstimatedTotalPrice=count*PricePerCard,
				LineItems=new List<PcmPricingLineItem> {
					new PcmPricingLineItem { Name="Postcard",Price=PricePerCard,Quantity=count,LineTotal=count*PricePerCard },
				},
			};
		}

		public PcmOrderResponse PlacePostcardOrder(PostcardManiaAccount account,PcmPostcardOrderRequest request) {
			CheckAccount(account);
			ListPlaceRequests.Add(request);
			if(ListOrders.Any(x => x.ExtRefNbr==request.ExtRefNbr)) {
				throw new PostcardManiaException(PostcardManiaErrorKind.Conflict,"An order with this reference was already placed.",HttpStatusCode.Conflict);
			}
			Exception exception=FuncPlaceFailure?.Invoke(request);
			if(exception!=null && !IsPlacedBeforeFailure) {
				throw exception;
			}
			PcmOrder order=new PcmOrder {
				OrderID=_orderIDNext++,
				BatchID=1,
				Status="Pending",
				OrderDate=DateTime.Today.ToString("yyyy-MM-dd"),
				DesignID=request.DesignID,
				ExtRefNbr=request.ExtRefNbr,
				MailClass=request.MailClass,
				RecipientCount=request.Recipients.Count,
			};
			ListOrders.Add(order);
			if(exception!=null) {
				throw exception;
			}
			return new PcmOrderResponse { OrderID=order.OrderID,BatchID=order.BatchID,ExtRefNbr=order.ExtRefNbr };
		}

		public PcmPagedResult<PcmOrder> GetOrders(PostcardManiaAccount account,string extRefNbr,int page,int perPage) {
			CheckAccount(account);
			if(!string.IsNullOrEmpty(extRefNbr)) {
				ListOrderLookups.Add(extRefNbr);
				return Page(ListOrders.Where(x => x.ExtRefNbr==extRefNbr).ToList(),page,perPage);
			}
			return Page(ListOrders.OrderByDescending(x => x.OrderID).ToList(),page,perPage);
		}

		public void CancelOrder(PostcardManiaAccount account,long orderID) {
			CheckAccount(account);
			PcmOrder order=ListOrders.FirstOrDefault(x => x.OrderID==orderID);
			if(order==null) {
				throw new PostcardManiaException(PostcardManiaErrorKind.NotFound,"Order not found.",HttpStatusCode.NotFound);
			}
			if(!order.IsCancelable) {
				throw new PostcardManiaException(PostcardManiaErrorKind.Validation,"The order can no longer be canceled.",HttpStatusCode.BadRequest);
			}
			order.Status="Canceled";
		}

		public PcmBatch GetBatch(PostcardManiaAccount account,long batchID) {
			CheckAccount(account);
			return new PcmBatch { BatchID=batchID,Status="Pending" };
		}

		public byte[] GetProofImage(string url) {
			return null;
		}

		private PcmDesign AddDesign(string name,string size) {
			PcmDesign design=new PcmDesign {
				DesignID=_designIDNext++,
				FriendlyName=name,
				Size=new PcmDesignSize { Key=size,Label=size },
				MailClasses=new List<string> { PcmMailClass.FirstClass,PcmMailClass.Standard },
			};
			ListDesigns.Add(design);
			return design;
		}

		private void CheckAccount(PostcardManiaAccount account) {
			if(account==null || !account.IsConfigured) {
				throw new PostcardManiaException(PostcardManiaErrorKind.NotConfigured,"No PostcardMania API key and secret are set up.");
			}
			if(FuncRejectAccount!=null && FuncRejectAccount(account)) {
				throw new PostcardManiaException(PostcardManiaErrorKind.CredentialsRejected,"The API key or secret was rejected.",HttpStatusCode.Unauthorized);
			}
		}

		private static PcmPagedResult<T> Page<T>(List<T> listItems,int page,int perPage) {
			page=Math.Max(1,page);
			perPage=Math.Max(1,perPage);
			int totalPages=Math.Max(1,(int)Math.Ceiling(listItems.Count/(double)perPage));
			return new PcmPagedResult<T> {
				Results=listItems.Skip((page-1)*perPage).Take(perPage).ToList(),
				Pagination=new PcmPagination { CurrentPage=page,PerPage=perPage,TotalPages=totalPages,TotalResults=listItems.Count },
			};
		}
	}
}
