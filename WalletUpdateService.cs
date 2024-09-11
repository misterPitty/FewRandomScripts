using System;
using UnityEngine;
using XSight.Scripts.Utils;

namespace XSight.Scripts.Data
{
    public class WalletUpdateService
    {
        public event Action<WalletModel> OnWalletUpdated;

        private NativeMessagesGetterService _messageGetter;
        private NativeMessagesSenderService _messageSender;

        [Inject]
        private void Construct(NativeMessagesGetterService messageGetter, NativeMessagesSenderService messageSender)
        {
            _messageGetter = messageGetter;
            _messageSender = messageSender;

            Init();
        }

        private void Init()
        {
            _messageGetter.OnNativeMessageGot += HandleNativeMessage;

            if (_messageGetter.LastMessages.TryGetValue(NativeMessageCommand.UpdateWallet, out string message))
            {
                HandleNativeMessage(NativeMessageCommand.UpdateWallet, message);
            }
            else
            {
                _messageSender.AskUpdateWallet();
            }
        }

        private void HandleNativeMessage(NativeMessageCommand cmd, string data)
        {
            if (cmd == NativeMessageCommand.UpdateWallet)
            {
                var model = JsonUtility.FromJson<WalletModel>(data);
                OnWalletUpdated?.Invoke(model);
            }
        }

        ~WalletUpdateService()
        {
            if (_messageGetter != null)
            {
                _messageGetter.OnNativeMessageGot -= HandleNativeMessage;
            }
        }
    }
}