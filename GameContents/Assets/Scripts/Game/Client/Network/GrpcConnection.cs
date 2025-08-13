﻿using Cysharp.Net.Http;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using UnityEngine;

namespace Game.Client.Network
{
    public static class GrpcConnection
    {
        public static GrpcChannel channel
        {
            get
            {
                if (!isInitialized)
                    InitChannel();

                return s_channel;
            }
        }

        public static bool isInitialized;
        public static string jwt;
        public static ClientInfo clientInfo;

        private static GrpcChannel s_channel;

        public static void InitChannel()
        {
            var connectionSettings = Resources.Load<ConnectionSettings>("Network/GrpcConnectionSettings");

            string url = $"http://{connectionSettings.serverIp}:{connectionSettings.serverPort}";

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var handler = new YetAnotherHttpHandler
            {
                Http2Only = true,
                SkipCertificateVerification = true, // 개발용
            };

            s_channel = GrpcChannel.ForAddress(url, new GrpcChannelOptions
            {
                HttpHandler = handler,
                Credentials = ChannelCredentials.Insecure, // 개발용 (비-암호화 연결)
                DisposeHttpClient = true, // Channel 해제시 HttpClient 도 같이 해제 옵션
            });

            isInitialized = true;
        }

        public static Metadata JwtHeader()
        {
            var meta = new Metadata();

            if (!string.IsNullOrEmpty(jwt))
                meta.Add("authorization", $"Bearer {jwt}");

            return meta;
        }
    }
}
