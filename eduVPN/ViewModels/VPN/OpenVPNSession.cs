﻿/*
    eduVPN - VPN for education and research

    Copyright: 2017-2021 The Commons Conservancy eduVPN Programme
    SPDX-License-Identifier: GPL-3.0+
*/

using eduOpenVPN;
using eduOpenVPN.Management;
using eduVPN.Models;
using eduVPN.ViewModels.Windows;
using Microsoft.Win32;
using Prism.Commands;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Web.Security;
using System.Windows.Threading;

namespace eduVPN.ViewModels.VPN
{
    /// <summary>
    /// OpenVPN session
    /// </summary>
    public class OpenVPNSession : Session
    {
        #region Fields

        /// <summary>
        /// OpenVPN Interactive Service instance name to connect to (e.g. "$eduVPN", "", etc.)
        /// </summary>
        private readonly string InstanceName;

        /// <summary>
        /// OpenVPN connection identifier
        /// </summary>
        /// <remarks>Connection identifier determines .conf and .log filenames.</remarks>
        private readonly string ConnectionId;

        /// <summary>
        /// OpenVPN working folder
        /// </summary>
        private readonly string WorkingFolder;

        /// <summary>
        /// Profile configuration
        /// </summary>
        private string ProfileConfig;

        /// <summary>
        /// Should HOLD hint on reconnect be ignored?
        /// </summary>
        private bool IgnoreHoldHint;

        /// <summary>
        /// Management Session
        /// </summary>
        private eduOpenVPN.Management.Session ManagementSession = new eduOpenVPN.Management.Session();

        /// <summary>
        /// Session renewal token
        /// </summary>
        private volatile bool RenewInProgress;

        /// <summary>
        /// Property update timer
        /// </summary>
        protected DispatcherTimer PropertyUpdater;

        #endregion

        #region Properties

        /// <summary>
        /// OpenVPN profile configuration file path
        /// </summary>
        string ConfigurationPath { get => WorkingFolder + ConnectionId + ".conf"; }

        /// <summary>
        /// OpenVPN connection log
        /// </summary>
        string LogPath { get => WorkingFolder + ConnectionId + ".txt"; }

        /// <summary>
        /// Client certificate
        /// </summary>
        private X509Certificate2 ClientCertificate
        {
            get { return _ClientCertificate; }
            set
            {
                if (SetProperty(ref _ClientCertificate, value))
                {
                    RaisePropertyChanged(nameof(ValidFrom));
                    RaisePropertyChanged(nameof(ValidTo));
                    RaisePropertyChanged(nameof(Expired));
                    RaisePropertyChanged(nameof(ExpiresTime));
                    RaisePropertyChanged(nameof(SuggestRenewal));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private X509Certificate2 _ClientCertificate;

        /// <inheritdoc/>
        public override DateTimeOffset ValidFrom { get => ClientCertificate != null ? ClientCertificate.NotBefore : DateTimeOffset.MinValue; }

        /// <inheritdoc/>
        public override DateTimeOffset ValidTo { get => ClientCertificate != null ? ClientCertificate.NotAfter : DateTimeOffset.MaxValue; }

        /// <inheritdoc/>
        public override bool Expired { get => ClientCertificate != null && ClientCertificate.NotAfter <= DateTimeOffset.UtcNow; }

        /// <inheritdoc/>
        public override TimeSpan ExpiresTime
        {
            get
            {
                return ClientCertificate != null ?
                    ClientCertificate.NotAfter - DateTimeOffset.UtcNow :
                    TimeSpan.MaxValue;
            }
        }

        /// <inheritdoc/>
        public override bool SuggestRenewal
        {
            get =>
                ClientCertificate != null &&
                (DateTimeOffset.UtcNow - ClientCertificate.NotBefore).Ticks >= 0.75 * (ClientCertificate.NotAfter - ClientCertificate.NotBefore).Ticks;
        }

        /// <inheritdoc/>
        public override DelegateCommand Renew
        {
            get
            {
                if (_Renew == null)
                {
                    _Renew = new DelegateCommand(
                        () =>
                        {
                            try
                            {
                                RenewInProgress = true;
                                _Renew.RaiseCanExecuteChanged();
                                new Thread(new ThreadStart(
                                    () =>
                                    {
                                        try
                                        {
                                            var cert = ConnectingProfile.Server.GetClientCertificate(
                                                Wizard.GetAuthenticatingServer(ConnectingProfile.Server),
                                                true,
                                                SessionAndWindowInProgress.Token);
                                            Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => ClientCertificate = cert));
                                            ManagementSession.SendSignal(SignalType.SIGHUP, SessionAndWindowInProgress.Token);
                                        }
                                        catch (OperationCanceledException) { }
                                        catch (Exception ex) { Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => Wizard.Error = ex)); }
                                        finally
                                        {
                                            RenewInProgress = false;
                                            Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => _Renew.RaiseCanExecuteChanged()));
                                        }
                                    })).Start();
                            }
                            catch (Exception ex) { Wizard.Error = ex; }
                        },
                        () => !RenewInProgress && State == SessionStatusType.Connected);
                    PropertyChanged += (object sender, PropertyChangedEventArgs e) => { if (e.PropertyName == nameof(State)) _Renew.RaiseCanExecuteChanged(); };
                }
                return _Renew;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DelegateCommand _Renew;

        /// <inheritdoc/>
        override public DelegateCommand ShowLog
        {
            get
            {
                if (_ShowLog == null)
                    _ShowLog = new DelegateCommand(
                        () =>
                        {
                            try
                            {
                                // Open log file in registered application.
                                Process.Start(LogPath);
                            }
                            catch (Exception ex) { Wizard.Error = ex; }
                        },
                        () => File.Exists(LogPath));
                return _ShowLog;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DelegateCommand _ShowLog;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an OpenVPN session
        /// </summary>
        /// <param name="instanceName">OpenVPN Interactive Service instance name to connect to (e.g. "$eduVPN", "", etc.)</param>
        /// <param name="wizard">The connecting wizard</param>
        /// <param name="connectingProfile">Connecting eduVPN profile</param>
        public OpenVPNSession(string instanceName, ConnectWizard wizard, Profile connectingProfile) :
            base(wizard, connectingProfile)
        {
            InstanceName = instanceName;
            ConnectionId = Guid.NewGuid().ToString();
            try
            {
                // Use OpenVPN configuration folder.
                using (var hklmKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (var key = hklmKey.OpenSubKey("SOFTWARE\\OpenVPN" + InstanceName, false))
                    {
                        WorkingFolder = key.GetValue("config_dir").ToString().TrimEnd();
                        string pathSeparator = Path.DirectorySeparatorChar.ToString();
                        if (!WorkingFolder.EndsWith(pathSeparator))
                            WorkingFolder += pathSeparator;
                        if (!Directory.Exists(WorkingFolder))
                            throw new FileNotFoundException();
                    }
                }
            }
            catch
            {
                // Use temporary folder.
                WorkingFolder = Path.GetTempPath();
            }

            // Create dispatcher timer to refresh properties and commands periodically.
            PropertyUpdater = new DispatcherTimer(
                new TimeSpan(0, 0, 0, 1),
                DispatcherPriority.Normal,
                (object sender, EventArgs e) =>
                {
                    RaisePropertyChanged(nameof(Expired));
                    RaisePropertyChanged(nameof(ExpiresTime));
                    RaisePropertyChanged(nameof(SuggestRenewal));
                    _ShowLog?.RaiseCanExecuteChanged();
                },
                Wizard.Dispatcher);
            PropertyUpdater.Start();

            PreRun.Add(() =>
            {
                // Get profile's OpenVPN configuration.
                ProfileConfig = ConnectingProfile.GetOpenVPNConfig(SessionAndWindowInProgress.Token);
            });

            PreRun.Add(() =>
            {
                // Get client certificate.
                var cert = ConnectingProfile.Server.GetClientCertificate(
                    Wizard.GetAuthenticatingServer(ConnectingProfile.Server),
                    false,
                    SessionAndWindowInProgress.Token);
                Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => ClientCertificate = cert));
            });

            // Set management session event handlers.
            ManagementSession.ByteCountReported += (object sender, ByteCountReportedEventArgs e) =>
                Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(
                    () =>
                    {
                        BytesIn = e.BytesIn;
                        BytesOut = e.BytesOut;
                    }));

            ManagementSession.FatalErrorReported += (object sender, MessageReportedEventArgs e) =>
                Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(
                    () =>
                    {
                        State = SessionStatusType.Error;
                        var msg = Resources.Strings.OpenVPNStateTypeFatalError;
                        if (e.Message != null && e.Message.Length > 0)
                        {
                            // Append OpenVPN message.
                            msg += "\r\n" + e.Message;
                        }
                        StateDescription = msg;

                        // Also, display the error message in the connect wizard.
                        Wizard.Error = new Exception(msg);
                    }));

            ManagementSession.HoldReported += (object sender, HoldReportedEventArgs e) =>
            {
                if (!IgnoreHoldHint && e.WaitHint > 0)
                    SessionAndWindowInProgress.Token.WaitHandle.WaitOne(e.WaitHint * 1000);
            };

            ManagementSession.RemoteReported += (object sender, RemoteReportedEventArgs e) =>
            {
                if (e.Protocol == ProtoType.UDP && Properties.Settings.Default.OpenVPNForceTCP)
                    e.Action = new RemoteSkipAction();
                else
                    e.Action = new RemoteAcceptAction();
            };

            ManagementSession.StateReported += (object sender, StateReportedEventArgs e) =>
            {
                Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(
                    () =>
                    {
                        var state = SessionStatusType.Error;
                        string msg = null;
                        switch (e.State)
                        {
                            case OpenVPNStateType.Connecting:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeConnecting;
                                break;

                            case OpenVPNStateType.Resolving:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeResolving;
                                break;

                            case OpenVPNStateType.TcpConnecting:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeTcpConnecting;
                                break;

                            case OpenVPNStateType.Waiting:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeWaiting;
                                break;

                            case OpenVPNStateType.Authenticating:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeAuthenticating;
                                break;

                            case OpenVPNStateType.GettingConfiguration:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeGettingConfiguration;
                                break;

                            case OpenVPNStateType.AssigningIP:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeAssigningIP;
                                break;

                            case OpenVPNStateType.AddingRoutes:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeAddingRoutes;
                                break;

                            case OpenVPNStateType.Connected:
                                state = SessionStatusType.Connected;
                                msg = Resources.Strings.OpenVPNStateTypeConnected;
                                break;

                            case OpenVPNStateType.Reconnecting:
                                state = SessionStatusType.Connecting;
                                msg = Resources.Strings.OpenVPNStateTypeReconnecting;
                                break;

                            case OpenVPNStateType.Exiting:
                                state = SessionStatusType.Disconnecting;
                                msg = Resources.Strings.OpenVPNStateTypeExiting;
                                break;
                        }
                        if (!string.IsNullOrEmpty(e.Message))
                        {
                            if (msg != null)
                            {
                                // Append OpenVPN message.
                                msg += "\r\n" + e.Message;
                            }
                            else
                            {
                                // Replace with OpenVPN message.
                                msg = e.Message;
                            }
                        }
                        else if (msg == null)
                            msg = "";
                        StateDescription = msg;
                        TunnelAddress = e.Tunnel;
                        IPv6TunnelAddress = e.IPv6Tunnel;
                        ConnectedAt = e.State == OpenVPNStateType.Connected ? (DateTimeOffset?)e.TimeStamp : null;

                        // Set State property last, as the whole world is listening on this property to monitor connectivity changes.
                        // It is important that we have IP addresses and other info already set before rising PropertyChanged event for State.
                        State = state;
                    }));

                if (e.State == OpenVPNStateType.Reconnecting)
                {
                    switch (e.Message)
                    {
                        case "connection-reset": // Connection was reset.
                            if (ClientCertificate.NotAfter <= DateTime.Now)
                            {
                                // Client certificate expired. Try with a new client certificate then.
                                goto case "tls-error";
                            }
                            goto default;

                        case "auth-failure": // Client certificate was deleted/revoked on the server side, or the user is disabled.
                        case "tls-error": // Client certificate is not compliant with this eduVPN server. Was eduVPN server reinstalled?
                            // Refresh client certificate.
                            var cert = ConnectingProfile.Server.GetClientCertificate(
                                Wizard.GetAuthenticatingServer(ConnectingProfile.Server),
                                true,
                                SessionAndWindowInProgress.Token);
                            Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => ClientCertificate = cert));
                            IgnoreHoldHint = true;
                            break;

                        default:
                            IgnoreHoldHint = false;
                            break;
                    }

                    ManagementSession.QueueReleaseHold(SessionAndWindowInProgress.Token);
                }
            };
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected override void DoRun()
        {
            Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => Wizard.TaskCount++));
            try
            {
                try
                {
                    // Start OpenVPN management interface on IPv4 loopack interface (any TCP free port).
                    var mgmtServer = new TcpListener(IPAddress.Loopback, 0);
                    mgmtServer.Start();
                    try
                    {
                        try
                        {
                            // Purge stale log files.
                            var timestamp = DateTime.UtcNow.Subtract(new TimeSpan(30, 0, 0, 0));
                            foreach (var f in Directory.EnumerateFiles(WorkingFolder, "*.txt", SearchOption.TopDirectoryOnly))
                            {
                                SessionAndWindowInProgress.Token.ThrowIfCancellationRequested();
                                if (File.GetLastWriteTimeUtc(f) <= timestamp)
                                {
                                    try { File.Delete(LogPath); }
                                    catch { }
                                }
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception) { /* Failure to remove stale log files is not fatal. */ }

                        try
                        {
                            // Save OpenVPN configuration file.
                            using (var fs = new FileStream(
                                ConfigurationPath,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.Read,
                                1048576,
                                FileOptions.SequentialScan))
                            using (var sw = new StreamWriter(fs))
                            {
                                // Save profile's configuration to file.

                                if (Properties.SettingsEx.Default.OpenVPNRemoveOptions is StringCollection openVPNRemoveOptions)
                                {
                                    // Remove options on the OpenVPNRemoveOptions list on the fly.
                                    using (var sr = new StringReader(ProfileConfig))
                                    {
                                        string inlineTerm = null;
                                        bool inlineRemove = false;
                                        for (; ; )
                                        {
                                            var line = sr.ReadLine();
                                            if (line == null)
                                                break;

                                            var trimmedLine = line.Trim();
                                            if (!string.IsNullOrEmpty(trimmedLine))
                                            {
                                                // Not an empty line.
                                                if (inlineTerm == null)
                                                {
                                                    // Not inside an inline option block = Regular parsing mode.
                                                    if (!trimmedLine.StartsWith("#") &&
                                                        !trimmedLine.StartsWith(";"))
                                                    {
                                                        // Not a comment.
                                                        var option = eduOpenVPN.Configuration.ParseParams(trimmedLine);
                                                        if (option.Count > 0)
                                                        {
                                                            if (option[0].StartsWith("<") && !option[0].StartsWith("</") && option[0].EndsWith(">"))
                                                            {
                                                                // Start of an inline option.
                                                                var o = option[0].Substring(1, option[0].Length - 2);
                                                                inlineTerm = "</" + o + ">";
                                                                inlineRemove = openVPNRemoveOptions.Contains(o);
                                                                if (inlineRemove)
                                                                {
                                                                    sw.WriteLine("# Commented by OpenVPNRemoveOptions setting:");
                                                                    line = "# " + line;
                                                                }
                                                            }
                                                            else if (openVPNRemoveOptions.Contains(option[0]))
                                                            {
                                                                sw.WriteLine("# Commented by OpenVPNRemoveOptions setting:");
                                                                line = "# " + line;
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // Inside an inline option block.
                                                    if (inlineRemove)
                                                    {
                                                        // Remove the inline option content.
                                                        line = "# " + line;
                                                    }

                                                    if (trimmedLine == inlineTerm)
                                                    {
                                                        // Inline option terminator found. Returning to regular parsing mode.
                                                        inlineTerm = null;
                                                    }
                                                }
                                            }

                                            sw.WriteLine(line);
                                        }
                                    }
                                }
                                else
                                    sw.Write(ProfileConfig);

                                // Append eduVPN Client specific configuration directives.
                                sw.WriteLine();
                                sw.WriteLine();
                                sw.WriteLine("# eduVPN Client for Windows");

                                // Introduce ourself (to OpenVPN server).
                                var assembly = Assembly.GetExecutingAssembly();
                                var assemblyTitleAttribute = Attribute.GetCustomAttributes(assembly, typeof(AssemblyTitleAttribute)).SingleOrDefault() as AssemblyTitleAttribute;
                                var assemblyVersion = assembly?.GetName()?.Version;
                                sw.WriteLine("setenv IV_GUI_VER " + eduOpenVPN.Configuration.EscapeParamValue(assemblyTitleAttribute?.Title + " " + assemblyVersion?.ToString()));

                                // Configure log file (relative to WorkingFolder).
                                sw.WriteLine("log-append " + eduOpenVPN.Configuration.EscapeParamValue(ConnectionId + ".txt"));

                                // Configure interaction between us and openvpn.exe.
                                sw.WriteLine("management " + eduOpenVPN.Configuration.EscapeParamValue(((IPEndPoint)mgmtServer.LocalEndpoint).Address.ToString()) + " " + eduOpenVPN.Configuration.EscapeParamValue(((IPEndPoint)mgmtServer.LocalEndpoint).Port.ToString()) + " stdin");
                                sw.WriteLine("management-client");
                                sw.WriteLine("management-hold");
                                sw.WriteLine("management-query-passwords");
                                sw.WriteLine("management-query-remote");

                                // Configure client certificate.
                                sw.WriteLine("cert " + eduOpenVPN.Configuration.EscapeParamValue(ConnectingProfile.Server.ClientCertificatePath));
                                sw.WriteLine("key " + eduOpenVPN.Configuration.EscapeParamValue(ConnectingProfile.Server.ClientCertificatePath));

                                // Ask when username/password is denied.
                                sw.WriteLine("auth-retry interact");
                                sw.WriteLine("auth-nocache");

                                // Set Wintun interface to be used.
                                sw.Write("windows-driver wintun\n");
                                sw.Write("dev-node " + eduOpenVPN.Configuration.EscapeParamValue(Properties.Settings.Default.ClientTitle) + "\n");

#if DEBUG
                                // Renegotiate data channel every 5 minutes in debug versions.
                                sw.WriteLine("reneg-sec 300");
#endif

                                if (Environment.OSVersion.Version < new Version(6, 2))
                                {
                                    // Windows 7 is using tiny 8kB send/receive socket buffers by default.
                                    // Increase to 64kB which is default from Windows 8 on.
                                    sw.WriteLine("sndbuf 65536");
                                    sw.WriteLine("rcvbuf 65536");
                                }

                                var openVPNAddOptions = Properties.SettingsEx.Default.OpenVPNAddOptions;
                                if (!string.IsNullOrWhiteSpace(openVPNAddOptions))
                                {
                                    sw.WriteLine();
                                    sw.WriteLine();
                                    sw.WriteLine("# Added by OpenVPNAddOptions setting:");
                                    sw.WriteLine(openVPNAddOptions);
                                }
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { throw new AggregateException(string.Format(Resources.Strings.ErrorSavingProfileConfiguration, ConfigurationPath), ex); }

                        bool retry;
                        do
                        {
                            retry = false;

                            // Connect to OpenVPN Interactive Service to launch the openvpn.exe.
                            using (var openvpnInteractiveServiceConnection = new eduOpenVPN.InteractiveService.Session())
                            {
                                var mgmtPassword = Membership.GeneratePassword(16, 6);
                                try
                                {
                                    openvpnInteractiveServiceConnection.Connect(
                                        string.Format("openvpn{0}\\service", InstanceName),
                                        WorkingFolder,
                                        new string[] { "--config", ConnectionId + ".conf", },
                                        mgmtPassword + "\n",
                                        3000,
                                        SessionAndWindowInProgress.Token);
                                }
                                catch (OperationCanceledException) { throw; }
                                catch (Exception ex) { throw new AggregateException(Resources.Strings.ErrorInteractiveService, ex); }

                                try
                                {
                                    // Wait and accept the openvpn.exe on our management interface (--management-client parameter).
                                    var mgmtClientTask = mgmtServer.AcceptTcpClientAsync();
                                    try { mgmtClientTask.Wait(30000, SessionAndWindowInProgress.Token); }
                                    catch (AggregateException ex) { throw ex.InnerException; }
                                    var mgmtClient = mgmtClientTask.Result;
                                    try
                                    {
                                        // Start the management session.
                                        ManagementSession.Start(mgmtClient.GetStream(), mgmtPassword, SessionAndWindowInProgress.Token);

                                        // Initialize session and release openvpn.exe to get started.
                                        ManagementSession.SetVersion(3, SessionAndWindowInProgress.Token);
                                        ManagementSession.ReplayAndEnableState(SessionAndWindowInProgress.Token);
                                        ManagementSession.ReplayAndEnableEcho(SessionAndWindowInProgress.Token);
                                        ManagementSession.SetByteCount(5, SessionAndWindowInProgress.Token);
                                        ManagementSession.ReleaseHold(SessionAndWindowInProgress.Token);

                                        Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => Wizard.TaskCount--));
                                        try
                                        {
                                            // Wait for the session to end gracefully.
                                            ManagementSession.Monitor.Join();
                                            if (ManagementSession.Error != null && !(ManagementSession.Error is OperationCanceledException))
                                            {
                                                // Session reported an error. Retry.
                                                retry = true;
                                            }
                                        }
                                        finally { Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() => Wizard.TaskCount++)); }
                                    }
                                    finally { mgmtClient.Close(); }
                                }
                                finally
                                {
                                    Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(
                                        () =>
                                        {
                                            // Cleanup status properties.
                                            State = SessionStatusType.Disconnecting;
                                            StateDescription = Resources.Strings.OpenVPNStateTypeExiting;
                                            TunnelAddress = null;
                                            IPv6TunnelAddress = null;
                                            ConnectedAt = null;
                                            BytesIn = null;
                                            BytesOut = null;
                                        }));

                                    // Wait for openvpn.exe to finish. Maximum 30s.
                                    try { Process.GetProcessById(openvpnInteractiveServiceConnection.ProcessId)?.WaitForExit(30000); }
                                    catch (ArgumentException) { }
                                }
                            }
                        } while (retry);
                    }
                    finally
                    {
                        mgmtServer.Stop();
                    }
                }
                finally
                {
                    // Delete profile configuration file. If possible.
                    try { File.Delete(ConfigurationPath); }
                    catch { }
                }
            }
            finally
            {
                Wizard.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(
                    () =>
                    {
                        // Cleanup status properties.
                        State = SessionStatusType.Disconnected;
                        StateDescription = "";

                        Wizard.TaskCount--;
                    }));
                PropertyUpdater.Stop();
            }
        }

        #endregion
    }
}
