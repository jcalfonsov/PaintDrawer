﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using PaintDrawer.Actions;
using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PaintDrawer.GMail
{
    class Account
    {
        public static double lastRead, lastSuccessfullRead, lastMailRecieved;

        public static UserCredential credential;
        public static GmailService service;

        public static void Init()
        {
            // GMail credentials should be stored in client_secret.json
            Console.ForegroundColor = Colors.Message;
            Console.WriteLine("[Gmail] Connecting to GMail API, loading credentials...");
            
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                String credPath = ".credentials/cred";

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new String[] { GmailService.Scope.GmailLabels, GmailService.Scope.GmailSettingsBasic, GmailService.Scope.GmailSettingsSharing, GmailService.Scope.GmailCompose, GmailService.Scope.GmailInsert, GmailService.Scope.GmailModify, GmailService.Scope.GmailReadonly, GmailService.Scope.GmailSend, GmailService.Scope.MailGoogleCom },
                    "me",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)
                ).Result;

                Console.ForegroundColor = Colors.Success;
                Console.WriteLine("[GMail] Success getting credentials!");
                Console.WriteLine("[GMail] Credential file saved to: " + credPath);
            }

            // Create Gmail API service.
            service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GMail API Quickstart",
            });

            Console.ForegroundColor = Colors.Success;
            Console.WriteLine("[GMail] GMail API successfully initiated & connected to Google and stuff");
        }

        public static List<RecievedMail> GetAllUnreadMails()
        {
            lastRead = Program.Time;
            List<RecievedMail> mails = new List<RecievedMail>(32);

            UsersResource.MessagesResource.ListRequest request = Account.service.Users.Messages.List("me");
            request.Q = "is:unread in:inbox";
            ListMessagesResponse response = request.Execute();

            if (response != null && response.Messages != null)
                for (int i = 0; i < response.Messages.Count; i++)
                    mails.Add(new RecievedMail(response.Messages[i].Id));

            lastRead = Program.Time;
            lastSuccessfullRead = lastRead;
            return mails;
        }

        public static bool ForceGetAllUnreadMails(int tries, out List<RecievedMail> list)
        {
            while(tries > 0)
            {
                try
                {
                    list = GetAllUnreadMails();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("[GMailAccount] ERROR: Coudln't read mails: (quote)\n" + e.Message + "(end quote).\nThere are " + tries + " tries left.");
                }
                tries--;
            }

            list = null;
            return false;
        }

        public static void TrashMail(String id)
        {
            UsersResource.MessagesResource.TrashRequest request = service.Users.Messages.Trash("me", id);
            Message message = request.Execute();
        }

        public static bool ForceTrashMail(String id, int tries)
        {
            while (tries > 0)
            {
                try
                {
                    TrashMail(id);
                    Console.ForegroundColor = Colors.Message;
                    Console.WriteLine("[GMail] Mail id=" + id + " trashed.");
                    return true;
                }
                catch
                {

                }
                tries--;
            }

            Console.ForegroundColor = Colors.Error;
            Console.WriteLine("[GMail] Couldn't trash mail id=" + id);
            return false;
        }

        /// <summary>
        /// Gets the new mails and adds new entries to the Queue.
        /// </summary>
        /// <param name="queue">... the fucking queue</param>
        public static void AddNewToQueue(Queue<IAction> queue)
        {
            List<RecievedMail> mails;
            if (ForceGetAllUnreadMails(5, out mails))
            {
                if (mails.Count == 0)
                {

                }
                else
                {
                    lastMailRecieved = Program.Time;

                    #region ProcessMails
                    for (int i=0; i<mails.Count; i++)
                    {
                        RecievedMail m = mails[i];

                        #region ProcessMail
                        m.LoadFull();
                        Console.ForegroundColor = Colors.Message;
                        Console.WriteLine("\n[GMail] Got mail from " + (m.From ?? "unknown") + "; subject: " + (m.Subject ?? "unknown"));

                        if (String.IsNullOrEmpty(m.Text))
                        {
                            Console.ForegroundColor = Colors.Error;
                            Console.WriteLine("[GMail] Couldn't interpret mail's body. Discarding :(");
                        }
                        else
                        {
                            if (m.From.Contains("tic") && m.From.Contains("ort.edu"))
                            {
                                #region DaroMail

                                Console.ForegroundColor = Colors.Message;
                                Console.WriteLine("[GMail] Mail from tic@ort detected, extracting content from Fw.");

                                int until = m.Text.Length-1; // Ignores characters until it sees no more spaces or new lines.
                                while ((m.Text[until] == ' ' || m.Text[until] == '\n' || m.Text[until] == '\r') && until > 0) until--;

                                // Searches for the end of the "Daro Forward" marked by an "Asunto: <subject>\r\n"
                                // and ignores characters until it finds something useful. (no spaces, \n or \r)
                                int start = m.Text.Length > 200 ? (m.Text.IndexOf('\n', m.Text.IndexOf("Asunto: ", 200))) : 0;
                                if (start != -1)
                                    while ((m.Text[start] == ' ' || m.Text[start] == '\n' || m.Text[start] == '\r') && start < m.Text.Length) start++;

                                String extract;
                                if (until > start && until < m.Text.Length && start >= 0)
                                {
                                    extract = m.Text.Substring(start, until - start + 1);
                                }
                                else
                                {
                                    until = m.Text.Length - 1;
                                    while ((m.Text[until] == ' ' || m.Text[until] == '\n' || m.Text[until] == '\r') && until > 0) until--;
                                    start = 0;
                                    while ((m.Text[start] == ' ' || m.Text[start] == '\n' || m.Text[start] == '\r') && start < m.Text.Length) start++;

                                    if (until > start && until > 0 && until < m.Text.Length && start > 0 && start < m.Text.Length)
                                    {
                                        Console.ForegroundColor = Colors.Message;
                                        Console.WriteLine("[GMail] The Message could not be extracted from the Fw. Utilizing it 'as is'.");
                                        extract = m.Text.Substring(start, until - start + 1);
                                    }
                                    else
                                    {
                                        Console.ForegroundColor = Colors.Error;
                                        Console.WriteLine("[GMail] Could not parse text from message. Discarding :(");
                                        extract = "";
                                    }
                                }

                                if (extract.Length != 0)
                                {
                                    Console.ForegroundColor = Colors.Success;
                                    Console.WriteLine("[GMail] Extracted: " + extract);
                                    _addWrite(queue, ref extract);
                                }
                                else
                                {
                                    Console.ForegroundColor = Colors.Message;
                                    Console.WriteLine("[GMail] Message discarded.");
                                }

                                #endregion
                            }
                            else
                            {
                                #region OtherMail

                                if (m.Subject.Contains("TEXT"))
                                {
                                    int until = m.Text.Length - 1;
                                    while ((m.Text[until] == ' ' || m.Text[until] == '\n' || m.Text[until] == '\r') && until > 0) until--;
                                    int start = 0;
                                    while ((m.Text[start] == ' ' || m.Text[start] == '\n' || m.Text[start] == '\r') && start < m.Text.Length) start++;

                                    if (until > start && start >= 0 && until < m.Text.Length)
                                    {

                                    }
                                }

                                #endregion
                            }
                        }
                        #endregion

                        //ForceTrashMail(m.Id, 5);
                    }
                    #endregion
                }
            }
            else
            {
                // if more than 3 minutes passed since it was able to read mails, we might have gotten disconnected...
                if (lastRead - lastSuccessfullRead > 3)
                {
                    Console.ForegroundColor = Colors.Error;
                    DateTime now = DateTime.Now;
                    Console.WriteLine("[GMail] Possibly lost connection. (" + now.Hour + ":" + now.Minute + ")");
                    queue.Enqueue(new SimpleWrite(Program.font, "Creo q me quede sin internet\n(" + now.Hour + ":" + now.Minute + ") (llamen a miz)"));
                }
            }
        }

        private static void _addWrite(Queue<IAction> queue, ref String text)
        {
            queue.Enqueue(new SimpleWrite(Program.font, text));
        }
    }
}
