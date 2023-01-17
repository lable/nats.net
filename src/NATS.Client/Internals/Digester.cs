// Copyright 2022 The NATS Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NATS.Client.Internals
{
    public class Digester
    {
#if NET40
        readonly SHA256 sha256 = SHA256Managed.Create();
        MemoryStream ms = default;
#else
        private IncrementalHash hasher;
        
#endif
        private string digest;
        private string entry;
#if NET40
public void AppendData(string s)
        {
            if (digest != null)
            {
                throw new InvalidOperationException("Digest has already been prepared.");
            }

            if (ms is null)
            {
                ms = new MemoryStream(1024);
            }

            var buf = Encoding.UTF8.GetBytes(s);
            ms.Write(buf, 0, buf.Length);
        }

        public void AppendData(byte[] data)
        {
            if (digest != null)
            {
                throw new InvalidOperationException("Digest has already been prepared.");
            }
            if (ms is null)
            {
                ms = new MemoryStream(1024);
            }

            ms.Write(data, 0, data.Length);
        }

        private void _prepareDigest()
        {
            if (digest == null && !(ms is null))
            {
                byte[] hash = ms.ToArray();
                ms.Position = 0;
                digest = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_');
                entry = "SHA-256=" + digest;
            }
        }

        
#else
        public Digester()
        {
            hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        }

        public void AppendData(string s)
        {
            if (digest != null)
            {
                throw new InvalidOperationException("Digest has already been prepared.");
            }
            hasher.AppendData(Encoding.UTF8.GetBytes(s));
        }

        public void AppendData(byte[] data)
        {
            if (digest != null)
            {
                throw new InvalidOperationException("Digest has already been prepared.");
            }
            hasher.AppendData(data);
        }

        private void _prepareDigest()
        {
            if (digest == null)
            {
                byte[] hash = hasher.GetHashAndReset();
                digest = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_');
                entry = "SHA-256=" + digest;
            }
        }
#endif

        public string GetDigestValue()
        {
            _prepareDigest();
            return digest;
        }

        public string GetDigestEntry()
        {
            _prepareDigest();
            return entry;
        }

        public bool DigestEntriesMatch(string thatEntry)
        {
            try
            {
                string thisEntry = GetDigestEntry();
                int at = thisEntry.IndexOf("=", StringComparison.Ordinal);
                return thisEntry.Substring(0, at).ToUpper().Equals(thatEntry.Substring(0, at).ToUpper()) 
                       && thisEntry.Substring(at).Equals(thatEntry.Substring(at));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}