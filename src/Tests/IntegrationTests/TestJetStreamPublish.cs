﻿// Copyright 2021 The NATS Authors
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
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NATS.Client;
using NATS.Client.JetStream;
using Xunit;
using Xunit.Abstractions;
using static UnitTests.TestBase;
using static IntegrationTests.JetStreamTestBase;

namespace IntegrationTests
{
    public class TestJetStreamPublish : TestSuite<JetStreamPublishSuiteContext>
    {
        private readonly ITestOutputHelper output;

        public TestJetStreamPublish(ITestOutputHelper output, JetStreamPublishSuiteContext context) : base(context)
        {
            this.output = output;
        }

        [Fact]
        public void TestJetStreamSimplePublish()
        {
            Context.RunInJsServer(c =>
            {
                CreateTestStream(c);

                IJetStream js = c.CreateJetStreamContext();

                PublishAck pa = js.Publish(SUBJECT, DataBytes());
                Assert.True(pa.HasError == false);
                Assert.True(pa.Seq == 1);
                Assert.Equal(STREAM, pa.Stream);
            });
        }

        [Fact]
        public void TestPublishVarieties() {
            Context.RunInJsServer(c =>
            {
                CreateTestStream(c);
                IJetStream js = c.CreateJetStreamContext();

                PublishAck pa = js.Publish(SUBJECT, DataBytes(1));
                AssertPublishAck(pa, 1);

                Msg msg = new Msg(SUBJECT, DataBytes(2));
                pa = js.Publish(msg);
                AssertPublishAck(pa, 2);

                PublishOptions po = PublishOptions.Builder().Build();
                pa = js.Publish(SUBJECT, DataBytes(3), po);
                AssertPublishAck(pa, 3);

                msg = new Msg(SUBJECT, DataBytes(4));
                pa = js.Publish(msg, po);
                AssertPublishAck(pa, 4);

                pa = js.Publish(SUBJECT, null);
                AssertPublishAck(pa, 5);

                msg = new Msg(SUBJECT);
                pa = js.Publish(msg);
                AssertPublishAck(pa, 6);

                pa = js.Publish(SUBJECT, null, po);
                AssertPublishAck(pa, 7);

                msg = new Msg(SUBJECT);
                pa = js.Publish(msg, po);
                AssertPublishAck(pa, 8);

                IJetStreamPushSyncSubscription s = js.PushSubscribeSync(SUBJECT);
                AssertNextMessage(s, Data(1));
                AssertNextMessage(s, Data(2));
                AssertNextMessage(s, Data(3));
                AssertNextMessage(s, Data(4));
                AssertNextMessage(s, null); // 5
                AssertNextMessage(s, null); // 6
                AssertNextMessage(s, null); // 7
                AssertNextMessage(s, null); // 8
            });
        }

        private void AssertNextMessage(IJetStreamPushSyncSubscription s, string data) {
            Msg m = s.NextMessage(DefaultTimeout);
            Assert.NotNull(m);
            if (data == null) {
                Assert.NotNull(m.Data);
                Assert.Empty(m.Data);
            }
            else {
                Assert.Equal(data, Encoding.ASCII.GetString(m.Data));
            }
        }

        [Fact(Skip = "WorkInProgress")]
        public void TestPublishAsyncVarieties()
        {
            Context.RunInJsServer(c =>
            {
                CreateTestStream(c);
                IJetStream js = c.CreateJetStreamContext();

                IList<Task<PublishAck>> tasks = new List<Task<PublishAck>>();
                
                tasks.Add(js.PublishAsync(SUBJECT, DataBytes(1)));

                Msg msg = new Msg(SUBJECT, DataBytes(2));
                tasks.Add(js.PublishAsync(msg));

                PublishOptions po = PublishOptions.Builder().Build();
                tasks.Add(js.PublishAsync(SUBJECT, DataBytes(3), po));

                msg = new Msg(SUBJECT, DataBytes(4));
                tasks.Add(js.PublishAsync(msg, po));

                IJetStreamPushSyncSubscription sub = js.PushSubscribeSync(SUBJECT);
                IList<Msg> list = ReadMessagesAck(sub);
                AssertContainsMessagesExact(list, 4);

                IList<ulong> seqnos = new List<ulong> {1, 2, 3, 4};
                foreach (var task in tasks)
                {
                    AssertContainsPublishAck(task.Result, seqnos);
                }

                AssertTaskException(js.PublishAsync(Subject(999), null));
                AssertTaskException(js.PublishAsync(new Msg(Subject(999))));

                PublishOptions pox = PublishOptions.Builder().WithExpectedLastMsgId(MessageId(999)).Build();
                AssertTaskException(js.PublishAsync(Subject(999), null, pox));
                AssertTaskException(js.PublishAsync(new Msg(Subject(999)), pox));
                
            });
        }

        private void AssertTaskException(Task<PublishAck> task)
        {
            try
            {
                var r = task.Result;
            }
            catch (Exception e)
            {
                Assert.NotNull(e.InnerException?.InnerException as NATSNoRespondersException);
            }
        }
        
        private void AssertContainsPublishAck(PublishAck pa, IList<ulong> seqnos) {
            Assert.Equal(STREAM, pa.Stream);
            Assert.False(pa.Duplicate);
            Assert.True(seqnos.Contains(pa.Seq));
            seqnos.Remove(pa.Seq);
        }

        private void AssertContainsMessagesExact(IList<Msg> list, int count)
        {
            Assert.Equal(4, list.Count);
            for (int x = 1; x <= count; x++)
            {
                bool found = false;
                string data = Data(x);
                foreach (var msg in list)
                {
                    if (data.Equals(Encoding.ASCII.GetString(msg.Data)))
                    {
                        found = true;
                        break;
                    }
                }
                Assert.True(found);
            }
        }

        [Fact]
        public void TestPublishExpectations()
        {
            Context.RunInJsServer(c =>
            {
                CreateTestStream(c);
                IJetStream js = c.CreateJetStreamContext();

                PublishOptions po = PublishOptions.Builder()
                    .WithExpectedStream(STREAM)
                    .WithMessageId(MessageId(1))
                    .Build();
                PublishAck pa = js.Publish(SUBJECT, DataBytes(1), po);
                AssertPublishAck(pa, 1);

                po = PublishOptions.Builder()
                    .WithExpectedLastMsgId(MessageId(1))
                    .WithMessageId(MessageId(2))
                    .Build();
                pa = js.Publish(SUBJECT, DataBytes(2), po);
                AssertPublishAck(pa, 2);

                po = PublishOptions.Builder()
                    .WithExpectedLastSequence(2)
                    .WithMessageId(MessageId(3))
                    .Build();
                pa = js.Publish(SUBJECT, DataBytes(3), po);
                AssertPublishAck(pa, 3);

                PublishOptions po1 = PublishOptions.Builder().WithExpectedStream(Stream(999)).Build();
                Assert.Throws<NATSJetStreamException>(() => js.Publish(SUBJECT, DataBytes(999), po1));

                PublishOptions po2 = PublishOptions.Builder().WithExpectedLastMsgId(MessageId(999)).Build();
                Assert.Throws<NATSJetStreamException>(() => js.Publish(SUBJECT, DataBytes(999), po2));

                PublishOptions po3 = PublishOptions.Builder().WithExpectedLastSequence(999).Build();
                Assert.Throws<NATSJetStreamException>(() => js.Publish(SUBJECT, DataBytes(999), po3));
            });
        }

        [Fact]
        public void TestPublishMiscExceptions()
        {
            Context.RunInJsServer(c =>
            {
                CreateTestStream(c);
                IJetStream js = c.CreateJetStreamContext();

                // stream supplied and matches
                PublishOptions po = PublishOptions.Builder()
                    .WithStream(STREAM)
                    .Build();
                js.Publish(SUBJECT, DataBytes(999), po);

                // mismatch stream to PO stream
                po = PublishOptions.Builder()
                    .WithStream(Stream(999))
                    .Build();
                Assert.Throws<NATSJetStreamException>(() => js.Publish(SUBJECT, DataBytes(999), po));

                // invalid subject
                Assert.Throws<NATSNoRespondersException>(() => js.Publish(Subject(999), DataBytes(999)));
            });
        }

        private void AssertPublishAck(PublishAck pa, ulong seqno) {
            Assert.Equal(STREAM, pa.Stream);
            Assert.Equal(seqno, pa.Seq);
            Assert.False(pa.Duplicate);
        }

        // TODO [Fact] public
        private void TestPublishNoAck()
        {
            Context.RunInJsServer(c =>
            {
                IJetStreamManagement jsm = c.CreateJetStreamManagementContext();
                
                StreamConfiguration sc = StreamConfiguration.Builder()
                    .WithName(STREAM)
                    .WithStorageType(StorageType.Memory)
                    .WithSubjects(SUBJECT)
                    .WithNoAck(true)
                    .Build();

                jsm.AddStream(sc);
                
                IJetStream js = c.CreateJetStreamContext();
                
                string data1 = "noackdata1";
                // string data2 = "noackdata2";

                PublishAck pa = js.Publish(SUBJECT, Encoding.ASCII.GetBytes(data1));
                Assert.Null(pa);

                // TODO
                // CompletableFuture<PublishAck> f = js.publishAsync(SUBJECT, data2.getBytes());
                // assertNull(f);

                IJetStreamPushSyncSubscription sub = js.PushSubscribeSync(SUBJECT);
                Msg m = sub.NextMessage(DefaultTimeout);
                Assert.NotNull(m);
                Assert.Equal(data1, Encoding.ASCII.GetString(m.Data));
                // m = sub.nextMessage(Duration.ofSeconds(2));
                // assertNotNull(m);
                // assertEquals(data2, new String(m.getData()));
                
            });
        }
    }
}