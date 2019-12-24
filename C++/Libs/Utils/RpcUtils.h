#pragma once

#include "../Domain/Event.h"
#include "../Blockchain/Transaction.h"

namespace phantasma {

const static BigInteger MinimumGasFee = 100000;
constexpr static int PaginationMaxResults = 50;
static const Char* PlatformName = "phantasma";
typedef void(FnCallback)(void);

enum class TransactionState
{
	Unknown,
	Rejected,
	Pending,
	Confirmed,
};

inline TransactionState CheckConfirmation(rpc::PhantasmaAPI& api, const Char* txHash, rpc::Transaction& output)
{
	rpc::PhantasmaError err;
	PHANTASMA_TRY
	{
		output = api.GetTransaction(txHash, &err);
	}
	PHANTASMA_CATCH( e )
	{
		err.code = 1;
		err.message = FromUTF8(e.what());
	}
	if( err.code == 0 )
		return TransactionState::Confirmed;
	else if( StringStartsWith(err.message, PHANTASMA_LITERAL("pending"), 7) )
		return TransactionState::Pending;
	else if( StringStartsWith(err.message, PHANTASMA_LITERAL("rejected"), 8) )
		return TransactionState::Rejected;
	else
		return TransactionState::Unknown;
}

inline TransactionState WaitForConfirmation(rpc::PhantasmaAPI& api, const Char* txHash, rpc::Transaction& output, FnCallback* fnSleep)
{
	for(;;)
	{
		TransactionState state = CheckConfirmation( api, txHash, output );
		switch(state)
		{
		case TransactionState::Confirmed:
		case TransactionState::Rejected:
			return state;
		default:
			if( fnSleep )
				fnSleep();
			break;
		}
	}
}

inline TransactionState SendTransaction(rpc::PhantasmaAPI& api, Transaction& tx, String& out_txHash)
{
	String rawTx = Base16::Encode(tx.ToByteArray(true));
	out_txHash = tx.GetHash().ToString();
	PHANTASMA_TRY
	{
		if( out_txHash == api.SendRawTransaction(rawTx.c_str()) )
			return TransactionState::Pending;
	}
	PHANTASMA_CATCH_ALL()
	{
	}
	return TransactionState::Unknown;
}

inline TransactionState SendTransaction(rpc::PhantasmaAPI& api, Transaction& tx)
{
	String txHash;
	return SendTransaction(api, tx, txHash);
}

inline TransactionState SendTransactionWaitConfirm(rpc::PhantasmaAPI& api, Transaction& tx, String& out_txHash, rpc::Transaction& out_confirmation, FnCallback* fnSleep)
{
	if( TransactionState::Unknown == SendTransaction(api, tx, out_txHash) )
		return TransactionState::Unknown;
	return WaitForConfirmation(api, out_txHash.c_str(), out_confirmation, fnSleep);
}

inline TransactionState SendTransactionWaitConfirm(rpc::PhantasmaAPI& api, Transaction& tx, FnCallback* fnSleep)
{
	String txHash;
	rpc::Transaction confirmation;
	return SendTransactionWaitConfirm(api, tx, txHash, confirmation, fnSleep);
}

// Did an address receive a particular token type from this transaction?
// Who sent it, how many tokens were received?
inline bool GetTxTokensReceived(
	BigInteger& out_value, 
	String& out_addressFrom, 
	const rpc::Transaction& tx, 
	const String& addressTo, 
	const Char* tokenSymbol = PHANTASMA_LITERAL("SOUL"),
	const Char* chainName = PHANTASMA_LITERAL("main")
	)
{
	if(!tokenSymbol || tokenSymbol[0] == '\0')
	{
		PHANTASMA_EXCEPTION("Invalid usage - must provide a token");
		return false;
	}
	BigInteger value;
	String receivedChain;
	for (const auto& evt : tx.events)
	{
		if( evt.address != addressTo )
			continue;
		EventKind eventKind = StringToEventKind(evt.kind);
		if(eventKind == EventKind::TokenReceive)
		{
			TokenEventData data = Serialization<TokenEventData>::Unserialize(Base16::Decode(evt.data));
			if( 0!=data.symbol.compare(tokenSymbol) )
				continue;
			if( chainName && 0!=data.chainName.compare(chainName) )
				continue;
			if( data.value.IsZero() )
				continue;
			receivedChain = data.chainName;
			value = data.value;
			break;
		}
	}
	if( value.IsZero() )
		return false;
	for (const auto& evt : tx.events)
	{
		EventKind eventKind = StringToEventKind(evt.kind);
		if(eventKind == EventKind::TokenSend)
		{
			TokenEventData data = Serialization<TokenEventData>::Unserialize(Base16::Decode(evt.data));
			if( 0!=data.symbol.compare(tokenSymbol) )
				continue;
			if( data.chainName != receivedChain )
				continue;
			if( value != data.value )
				continue;
			out_value = value;
			out_addressFrom = evt.address;
			return true;
		}
	}
	return false;
}


inline bool GetTxTokensSent(
	BigInteger& out_value, 
	String& out_addressTo, 
	const rpc::Transaction& tx, 
	const String& addressFrom, 
	const Char* tokenSymbol = PHANTASMA_LITERAL("SOUL"),
	const Char* tokenIgnore = PHANTASMA_LITERAL("KCAL"),
	const Char* chainName = PHANTASMA_LITERAL("main")
)
{
	for (int i=0, end=(int)tx.events.size(); i!=end; ++i)
	{
		const auto& evtA = tx.events[i];
		if( evtA.address != addressFrom )
			continue;
		EventKind eventKind = StringToEventKind(evtA.kind);
		if(eventKind == EventKind::TokenSend)
		{
			TokenEventData sendData = Serialization<TokenEventData>::Unserialize(Base16::Decode(evtA.data));
			if( tokenSymbol && 0!=sendData.symbol.compare(tokenSymbol) )
				continue;
			if( tokenIgnore && 0==sendData.symbol.compare(tokenIgnore) )
				continue;
			if( chainName && 0!=sendData.chainName.compare(chainName) )
				continue;
			if( sendData.value.IsZero() )
				continue;

			for (int j=i+1; j<end; ++j)
			{
				const auto& evtB = tx.events[j];
				EventKind eventKind = StringToEventKind(evtB.kind);
				if(eventKind == EventKind::TokenReceive)
				{
					TokenEventData rcvData = Serialization<TokenEventData>::Unserialize(Base16::Decode(evtB.data));
					if( rcvData.symbol != sendData.symbol ||
						rcvData.chainName != sendData.chainName ||
						rcvData.value != sendData.value )
						continue;
					out_value = rcvData.value;
					out_addressTo = evtB.address;
					return true;
				}
			}
		}
	}
	return false;
}


inline bool GetTxTokensMinted(
	const rpc::Transaction& tx,
	PHANTASMA_VECTOR<TokenEventData>* output
)
{
	bool any = false;
	for (int i=0, end=(int)tx.events.size(); i!=end; ++i)
	{
		const auto& evt = tx.events[i];
		if( 0 == evt.kind.compare(PHANTASMA_LITERAL("TokenMint")) )
		{
			if( output )
				output->push_back(Serialization<TokenEventData>::Unserialize(Base16::Decode(evt.data)));
			any = true;
		}
	}
	return any;
}

inline String GetTxDescription(
	const rpc::Transaction& tx, 
	const PHANTASMA_VECTOR<rpc::Chain>& phantasmaChains, 
	const PHANTASMA_VECTOR<rpc::Token>& phantasmaTokens, 
	const String& addressFrom = String{})
{
	StringBuilder sb;
	String description;

	String senderToken;
	String senderChain;
	for(const auto& x : phantasmaChains)
	{
		if( x.address == tx.chainAddress )
		{
			senderChain = x.name;
			break;
		}
	}
	Address senderAddress;

	String receiverToken;
	String receiverChain;
	Address receiverAddress;

	BigInteger amount;

	for(const auto& evt : tx.events)
	{
		auto eventKind = StringToEventKind(evt.kind);
		switch (eventKind)
		{

		case EventKind::TokenClaim:
		{
			TokenEventData data = Serialization<TokenEventData>::Unserialize(Base16::Decode(evt.data));
			if (0==data.symbol.compare(PHANTASMA_LITERAL("SOUL")))
			{
				return String{PHANTASMA_LITERAL("Custom transaction")};
			}
		}
		break;


		case EventKind::TokenStake:
		{
			TokenEventData data = Serialization<TokenEventData>::Unserialize(Base16::Decode(evt.data));
			amount = data.value;
			if (amount >= 1000000000)
			{
				if (0!=data.symbol.compare(PHANTASMA_LITERAL("KCAL")) &&
					0!=data.symbol.compare(PHANTASMA_LITERAL("NEO")) &&
					0!=data.symbol.compare(PHANTASMA_LITERAL("GAS")) )
				{
					//return String{PHANTASMA_LITERAL("Stake transaction")};
					return String{PHANTASMA_LITERAL("Custom transaction")};
				}
			}
		}
		break;

		case EventKind::TokenMint:
		{
			return String{PHANTASMA_LITERAL("Claim transaction")};
		}
		break;

		case EventKind::AddressRegister:
		{
			auto name = Serialization<String>::Unserialize(Base16::Decode(evt.data));
			sb << PHANTASMA_LITERAL("Register transaction: name '");
			sb << name;
			sb << PHANTASMA_LITERAL("' registered");
			description = sb.str();
		}
		break;

		case EventKind::TokenSend:
		{
			TokenEventData data = Serialization<TokenEventData>::Unserialize(Base16::Decode(evt.data));
			amount = data.value;
			senderAddress = Address::FromText(evt.address);
			senderToken = data.symbol;
		}
		break;

		case EventKind::TokenReceive:
		{
			TokenEventData data = Serialization<TokenEventData>::Unserialize(Base16::Decode(evt.data));
			amount = data.value;
			receiverAddress = Address::FromText(evt.address);
			receiverChain = data.chainName;
			receiverToken = data.symbol;
		}
		break;

		}
	}

	if (description.empty())
	{
		if (amount > 0 && !senderAddress.IsNull() && !receiverAddress.IsNull() &&
			!senderToken.empty() && senderToken == receiverToken)
		{
			//auto amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == senderToken).Decimals);
			if(addressFrom.empty())
			{
				sb << PHANTASMA_LITERAL("Receive transaction: from ");
				sb << senderAddress.ToString();
				sb << PHANTASMA_LITERAL(" to ");
				sb << receiverAddress.ToString();
				description = sb.str();
			}
			else if (addressFrom == senderAddress.ToString())
			{
				sb << PHANTASMA_LITERAL("Send transaction: to ");
				sb << receiverAddress.ToString();
				description = sb.str();
			}
			else
			{
				sb << PHANTASMA_LITERAL("Receive transaction: from ");
				sb << senderAddress.ToString();
				description = sb.str();
			}

		}
		else if (amount > 0 && !receiverAddress.IsNull() && !receiverToken.empty())
		{
			//Int32 decimals = -1;
			//for(const auto& p : phantasmaTokens)
			//{
			//	if(p.symbol == receiverToken)
			//	{
			//		decimals = p.decimals;
			//		break;
			//	}
			//}
			//if(decimals < 0)
			//{
			//	PHANTASMA_EXCEPTION("Unknown token encountered");
			//}
			//var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == receiverToken).Decimals);

			sb << PHANTASMA_LITERAL("Send transaction: to ");
			sb << receiverAddress.Text();
			sb << PHANTASMA_LITERAL(" ");
			description = sb.str();
		}
		else
		{
			description = PHANTASMA_LITERAL("Custom transaction");
		}

	}

	return description;
}

}
