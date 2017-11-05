using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace Namiono
{
	public enum Realm
	{
		Public,
		Private,
		api,
		Admin
	}

	public class WebSite<T> : IDisposable
	{
		WebServer ws;
		Filesystem fs;
		SQLDatabase<T> db;
		Users<T> users;

		Dictionary<Guid, Sendeplan<T>> SendePlaene;
		Dictionary<string, ShoutcastServer<T>> ShoutcastServers;

		Realm realm;
		string design;
		string title;

		public WebSite(ref SQLDatabase<T> db, string name, string title, ushort port, Realm realm = Realm.Public, string design = "default")
		{
			this.title = title;
			this.db = db;
			this.design = design;
			this.realm = realm;

			fs = new Filesystem(Path.Combine(Environment.CurrentDirectory, string.Format("{0}_website", name)));

			users = new Users<T>(ref db);

			if (this.realm == Realm.Public)
			{
				ShoutcastServers = new Dictionary<string, ShoutcastServer<T>>();
				SendePlaene = new Dictionary<Guid, Sendeplan<T>>();

				LoadShoutcastServers();
				LoadSendePlan();
			}

			ws = new WebServer(this.title, ref fs, port);
			ws.DirectRequestReceived += (sender, e) =>
			{
				var cook = e.Context.Request.Cookies["User"] != null ?
				e.Context.Request.Cookies["User"].Value : "0";

				if (string.IsNullOrEmpty(cook))
					cook = "0";

				var p = e.Path.Split('.')[0].ToLower();
				if (p.StartsWith("/"))
					p = p.Remove(0, 1);

				var userid = (T)Convert.ChangeType(ulong.Parse(cook), typeof(T));
				var tpl = Dispatcher.ReadTemplate(ref fs, "content_box");

				if (e.Target == WebServer.RequestTarget.Provider)
				{
					switch (e.Provider)
					{
						case WebServer.Provider.Admin:
							switch (e.Action)
							{
								case WebServer.SiteAction.Add:
									break;
								case WebServer.SiteAction.Edit:
									break;
								case WebServer.SiteAction.Remove:
									break;
								case WebServer.SiteAction.Login:
									break;
								case WebServer.SiteAction.Logout:
									break;
								case WebServer.SiteAction.None:
									break;
								case WebServer.SiteAction.Show:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										if (e.Params.ContainsKey("admincp") && e.Params["admincp"] == "yes" && users.CanSeeAdminCenter(userid))
										{
											var site_resp = Dispatcher.ReadTemplate(ref fs, "content-box")
											.Replace("[[content]]", "admincp").Replace("[[box-title]]", "Verwaltungs-Center");

											if (users.CanSeeSiteSettings(userid))
											{
												var site_admincp_site = Dispatcher.ReadTemplate(ref fs, "site-admincp-site").Replace("[[content]]", "admincp_site");
												site_resp = site_resp.Replace("[[box-content]]", site_admincp_site);
											}

											e.Context.Response.StatusCode = 200;
											e.Context.Response.StatusDescription = "OK";
											e.Context.Response.ContentType = "text/html";

											ws.Send(ref site_resp, ref e.Context, Encoding.UTF8);
										}
										else
										{
											var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
											ws.Send(ref err, ref e.Context);
										}
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}

									break;
								case WebServer.SiteAction.MetaData:
									break;
								case WebServer.SiteAction.Clients:
									break;
								case WebServer.SiteAction.Profile:
									break;
								case WebServer.SiteAction.Register:
									break;
							}

							break;
						case WebServer.Provider.Shoutcast:
							switch (e.Action)
							{
								case WebServer.SiteAction.Add:
									break;
								case WebServer.SiteAction.Edit:
									break;
								case WebServer.SiteAction.Remove:
									break;
								case WebServer.SiteAction.MetaData:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var output = string.Empty;
										lock (ShoutcastServers)
										{
											foreach (var sc in ShoutcastServers.Values)
											{
												if (sc == null)
													continue;

												output += sc.OutPut(userid);
											}

											output = output.Replace("[#DESIGN#]", this.design);
										}

										e.Context.Response.StatusCode = 200;
										e.Context.Response.StatusDescription = "OK";
										e.Context.Response.ContentType = "text/html";

										ws.Send(ref output, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Clients:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var output = string.Empty;
										if (e.Params.ContainsKey("show") && e.Params.ContainsKey("sid") && e.Params["show"] == "clients")
										{
											lock (ShoutcastServers)
											{
												foreach (var sc in ShoutcastServers.Values)
													output += sc.Members[(T)Convert.ChangeType(int.Parse(e.Params["sid"]), typeof(T))].Clients(userid);
											}
										}
                                        else
                                        {
                                            var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
                                            ws.Send(ref err, ref e.Context);
                                        }

                                        e.Context.Response.StatusCode = 200;
										e.Context.Response.StatusDescription = "OK";
										e.Context.Response.ContentType = "text/html";

										ws.Send(ref output, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Login:
									break;
								case WebServer.SiteAction.Execute:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										if (e.Params.ContainsKey("param"))
										{
											var resp = string.Empty;
											var result = Filesystem.ExecuteFile("/etc/init.d/shoutcast", e.Params["param"]);
											if (result == 0)
											{
												e.Context.Response.StatusCode = 200;
												e.Context.Response.StatusDescription = "OK";
												e.Context.Response.ContentType = "text/html";
											}
											else
											{
												e.Context.Response.StatusCode = 200;
												e.Context.Response.StatusDescription = "Process failed to start!";
												e.Context.Response.ContentType = "text/html";
											}
											ws.Send(ref resp, ref e.Context, Encoding.UTF8);
										}
										else
										{
											var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
											ws.Send(ref err, ref e.Context);
										}
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Show:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										if (e.Params.ContainsKey("show") && e.Params["show"] == "streams")
										{
											var output = string.Empty;
											lock (ShoutcastServers)
											{
												foreach (var sc in ShoutcastServers.Values)
													output += sc.ListCurrentStreams();
											}

											e.Context.Response.StatusCode = 200;
											e.Context.Response.StatusDescription = "OK";
											e.Context.Response.ContentType = "text/html";

											ws.Send(ref output, ref e.Context, Encoding.UTF8);
										}
                                        else
                                        {
                                            var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
                                            ws.Send(ref err, ref e.Context);
                                        }
                                    }
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}
									break;
								default:
									lock (ShoutcastServers)
									{
										if (e.Params.ContainsKey("player") && e.Params.ContainsKey("id") && e.Params.ContainsKey("serverid") &&
											ShoutcastServers.ContainsKey(e.Params["serverid"]))
										{
											var sc = ShoutcastServers[e.Params["serverid"]];

											var player = e.Params["player"];
											var id = (T)Convert.ChangeType(int.Parse(e.Params["id"]), typeof(T));

											if (player == "web" && sc.Members.ContainsKey(id))
											{
												var pl_resp = Dispatcher.ReadTemplate(ref fs, "static_site")
												.Replace("[#CONTENT#]", sc.Webplayer(id)).Replace("[#SITE_TITLE#]", "WEB-Player").Replace("[#DESIGN#]", this.design); ;

												e.Context.Response.ContentType = "text/html";
												e.Context.Response.StatusCode = 200;
												e.Context.Response.StatusDescription = "OK";

												ws.Send(ref pl_resp, ref e.Context, Encoding.UTF8);
											}

											if (player == "asx" && sc.Members.ContainsKey(id))
											{
												var output = "<asx version=\"3.0\">";
												output += string.Format("<title>{0}</title>", sc.Members[id].ServerName);
												output += "<entry>";
												output += string.Format("\t<title>{0}</title>", sc.Members[id].ServerName);
												output += string.Format("\t<ref href=\"{0}\" />", sc.Members[id].StreamPath);

												output += "</entry>";
												output += "</asx>";
												e.Context.Response.ContentType = "video/x-ms-asf";
												e.Context.Response.Headers.Add("Content-Disposition", "attachment; filename=listen.asx");

												ws.Send(ref output, ref e.Context, Encoding.UTF8);
											}

											if (player == "pls" && sc.Members.ContainsKey(id))
											{
												var output = "[playlist]\r\n";
												output += "NumberOfEntries=1\r\n";
												output += string.Format("File1={0};\r\n", sc.Members[id].StreamPath);
												output += string.Format("Title1={0}\r\n", sc.Members[id].ServerName);
												output += "Length1=-1\r\n";
												output += "Version1=2\r\n";

												e.Context.Response.ContentType = "audio/x-scpls";
												e.Context.Response.Headers.Add("Content-Disposition", "attachment; filename=listen.pls");

												ws.Send(ref output, ref e.Context, Encoding.UTF8);
											}

											if (player == "ram" && sc.Members.ContainsKey(id))
											{
												var output = string.Format("{0}", sc.Members[id].StreamPath);

												e.Context.Response.ContentType = "audio/x-pn-realaudio";
												e.Context.Response.Headers.Add("Content-Disposition", "attachment; filename=listen.ram");

												ws.Send(ref output, ref e.Context, Encoding.UTF8);
											}

											if (player == "m3u" && sc.Members.ContainsKey(id))
											{
												var output = string.Format("#EXTM3U\r\n#EXTINF:-1,{0}\r\n{1}\r\n",
													sc.Members[id].ServerName, sc.Members[id].StreamPath);

												e.Context.Response.ContentType = "audio/x-mpegurl";
												e.Context.Response.Headers.Add("Content-Disposition", "attachment; filename=listen.m3u");

												ws.Send(ref output, ref e.Context, Encoding.UTF8);
											}
										}
										else
										{
											var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
											ws.Send(ref err, ref e.Context);
										}
									}

									break;
							}
							break;
						case WebServer.Provider.Sendeplan:
							switch (e.Action)
							{
								case WebServer.SiteAction.Add:
									switch (e.Method)
									{
										case WebServer.HTTPMethod.POST:

											if (e.Params.ContainsKey("day") && e.Params.ContainsKey("desc") &&
											e.Params.ContainsKey("hour") && e.Params.ContainsKey("user") && e.Params.ContainsKey("spident"))
											{
												var resp = string.Empty;

												var spident = Guid.Parse(e.Params["spident"]);

												lock (SendePlaene)
												{
													if (!SendePlaene.ContainsKey(spident))
													{
														e.Context.Response.StatusCode = 404;
														e.Context.Response.StatusDescription = "Der angeforderte Sendeplan ist (momentan) nicht verfügbar!";
													}
													else
													{
														if (SendePlaene[spident].Insert_SPEntry(e.Params["day"], e.Params["hour"], e.Params["timestamp"],
															e.Params["user"], e.Params["desc"], userid))
														{
															if (SendePlaene.Count != 1)
															{
																var output_sp = string.Empty;

																foreach (var sp in SendePlaene)
																	output_sp += string.Format("\t<option value=\"{0}\">{1}</option>\n", sp.Key, sp.Value.Name);

																resp = Dispatcher.ReadTemplate(ref fs, "sp_select").Replace("[#SPIDENT_LIST#]", output_sp);
															}

															resp += SendePlaene[spident].GetCurWeekPlan(userid);

															e.Context.Response.StatusCode = 200;
															e.Context.Response.StatusDescription = "OK!";
														}
														else
														{
															e.Context.Response.StatusCode = 500;
															e.Context.Response.StatusDescription = "Es trat ein Fehler beim Eintragen der Sendung auf!";
														}

													}
												}

												e.Context.Response.ContentType = "text/html";
												ws.Send(ref resp, ref e.Context, Encoding.UTF8);
											}
											else
											{
												var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
												ws.Send(ref err, ref e.Context);
											}
											break;
										case WebServer.HTTPMethod.GET:
											if (e.Params.ContainsKey("day") && e.Params.ContainsKey("spident"))
											{
												var day = int.Parse(e.Params["day"]);
												var spident = Guid.Parse(e.Params["spident"]);
												var resp = string.Empty;

												if (SendePlaene.ContainsKey(spident))
												{
													resp = SendePlaene[spident].Add_Form(day, userid);

													e.Context.Response.StatusCode = 200;
													e.Context.Response.StatusDescription = "OK";
												}
												else
												{
													e.Context.Response.StatusCode = 404;
													e.Context.Response.StatusDescription = "Der angeforderte Sendeplan existiert nicht (mehr)!";
												}

												e.Context.Response.ContentType = "text/html";
												ws.Send(ref resp, ref e.Context, Encoding.UTF8);
											}
											else
											{
												var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
												ws.Send(ref err, ref e.Context);
											}
											break;
									}
									break;
								case WebServer.SiteAction.Edit:
									switch (e.Method)
									{
										case WebServer.HTTPMethod.POST:
											if (e.Params.ContainsKey("day") && e.Params.ContainsKey("desc") &&
											e.Params.ContainsKey("hour") && e.Params.ContainsKey("user") && e.Params.ContainsKey("spident"))
											{
												var resp = string.Empty;

												var spident = Guid.Parse(e.Params["spident"]);

												lock (SendePlaene)
												{
													if (SendePlaene.ContainsKey(spident))
													{
														if (SendePlaene[spident].Update_SPEntry(e.Params["day"], e.Params["hour"], e.Params["ts"], e.Params["user"], e.Params["desc"], userid))
														{
															if (SendePlaene.Count >= 1)
															{
																var output_sp = string.Empty;

																foreach (var spe in SendePlaene)
																	output_sp += string.Format("\t<option value=\"{0}\">{1}</option>\n", spe.Key, spe.Value.Name);

																resp = Dispatcher.ReadTemplate(ref fs, "sp_select").Replace("[#SPIDENT_LIST#]", output_sp);
															}

															resp += SendePlaene[spident].GetCurWeekPlan(userid);
															e.Context.Response.StatusCode = 200;
															e.Context.Response.StatusDescription = "OK";

															e.Context.Response.ContentType = "text/html";
															ws.Send(ref resp, ref e.Context, Encoding.UTF8);
														}
														else
														{
															e.Context.Response.StatusCode = 500;
															e.Context.Response.StatusDescription = "Fehler bei der bearbeitung der Sendung!";
															e.Context.Response.ContentType = "text/html";
															ws.Send(ref resp, ref e.Context, Encoding.UTF8);
														}
													}
												}


											}
											else
											{
												var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
												ws.Send(ref err, ref e.Context);
											}
											break;
										case WebServer.HTTPMethod.GET:
											if (e.Context.Request.Headers["UAgent"] == "Namiono")
											{
												if (e.Params.ContainsKey("day") && e.Params.ContainsKey("hour") && e.Params.ContainsKey("ts") && e.Params.ContainsKey("spident"))
												{
													var resp = string.Empty;
													var spident = Guid.Parse(e.Params["spident"]);

													if (SendePlaene.ContainsKey(spident))
													{
														resp = SendePlaene[spident].Edit_Form(int.Parse(e.Params["day"]), byte.Parse(e.Params["hour"]), double.Parse(e.Params["ts"]), userid);
														if (!string.IsNullOrEmpty(resp))
														{
															e.Context.Response.StatusCode = 200;
															e.Context.Response.StatusDescription = "OK";
														}
														else
														{
															e.Context.Response.StatusCode = 404;
															e.Context.Response.StatusDescription = "Die angeforderte Sendung existiert nicht (mehr)!";
														}
													}
													else
													{
														e.Context.Response.StatusCode = 404;
														e.Context.Response.StatusDescription = "Der angeforderte Sendeplan existiert nicht (mehr)!";
													}

													e.Context.Response.ContentType = "text/html";
													ws.Send(ref resp, ref e.Context, Encoding.UTF8);
												}
												else
												{
													var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
													ws.Send(ref err, ref e.Context);
												}
											}
											else
											{
												var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
												ws.Send(ref err, ref e.Context);
											}
											break;
										default:
											break;
									}


									break;
								case WebServer.SiteAction.Remove:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										if (e.Params.ContainsKey("day") && e.Params.ContainsKey("hour") && e.Params.ContainsKey("ts") && e.Params.ContainsKey("spident"))
										{
											if (!string.IsNullOrEmpty(e.Params["day"]) && !string.IsNullOrEmpty(e.Params["hour"]) && !string.IsNullOrEmpty(e.Params["ts"]))
											{
												var resp = string.Empty;

												if (users.CanDeleteSendeplan(userid))
												{
													var spident = Guid.Parse(e.Params["spident"]);
													if (SendePlaene[spident].DeleteSPEntry(double.Parse(e.Params["day"]), byte.Parse(e.Params["hour"]), double.Parse(e.Params["ts"])))
													{
														e.Context.Response.StatusCode = 200;
														e.Context.Response.StatusDescription = "OK";

														resp = SendePlaene[spident].GetCurWeekPlan(userid);
													}
													else
													{
														e.Context.Response.StatusCode = 404;
														e.Context.Response.StatusDescription = "Fehler beim löschen der Sendung. (Wurde die Sendung wurde bereits gelöscht?).";
													}
												}
												else
												{
													e.Context.Response.StatusCode = 403;
													e.Context.Response.StatusDescription = "Keine Berechtigung";
												}

												e.Context.Response.ContentType = "text/html";
												ws.Send(ref resp, ref e.Context, Encoding.UTF8);
											}
										}
										else
										{
											var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
											ws.Send(ref err, ref e.Context);
										}
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Login:
									break;
								case WebServer.SiteAction.Show:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var resp = string.Empty;
										if (SendePlaene.Count == 0)
										{
											e.Context.Response.StatusCode = 500;
											e.Context.Response.StatusDescription = "Der Provider enthält keine Sendepläne!";
										}
										else
										{
											if (e.Params.ContainsKey("spident"))
											{
												var spident = Guid.Parse(e.Params["spident"]);

												if (!SendePlaene.ContainsKey(spident))
												{
													e.Context.Response.StatusCode = 500;
													e.Context.Response.StatusDescription = "Der Provider ist nicht verfügbar!";
												}
												else
												{
													e.Context.Response.StatusCode = 200;
													e.Context.Response.StatusDescription = "OK";

													if (SendePlaene.Count != 1)
													{
														var output_sp = string.Empty;

														foreach (var sp in SendePlaene)
															output_sp += string.Format("\t<option value=\"{0}\">{1}</option>\n", sp.Key, sp.Value.Name);

														resp = Dispatcher.ReadTemplate(ref fs, "sp_select").Replace("[#SPIDENT_LIST#]", output_sp);
													}

													resp += SendePlaene[spident].GetCurWeekPlan(userid);
												}
											}
											else
											{
												if (SendePlaene.Count != 1)
												{
													var output_sp = string.Empty;

													foreach (var sp in SendePlaene)
														output_sp += string.Format("\t<option value=\"{0}\">{1}</option>\n", sp.Key, sp.Value.Name);

													resp = Dispatcher.ReadTemplate(ref fs, "sp_select").Replace("[#SPIDENT_LIST#]", output_sp);
												}

												resp += (from s in SendePlaene.Values select s).FirstOrDefault().GetCurWeekPlan(userid);
											}
										}

										e.Context.Response.ContentType = "text/html";
										ws.Send(ref resp, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.None:
									break;
							}
							break;
						case WebServer.Provider.User:
							switch (e.Action)
							{
								case WebServer.SiteAction.Register:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var resp = users.GetRegisterForm(ref fs);

										e.Context.Response.StatusCode = 200;
										e.Context.Response.StatusDescription = "OK";

										e.Context.Response.ContentType = "text/html";
										ws.Send(ref resp, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										ws.Send(ref err, ref e.Context);
									}

									break;
								case WebServer.SiteAction.Add:
									switch (e.Method)
									{
										case WebServer.HTTPMethod.POST:

											if (e.Params.ContainsKey("username") && e.Params.ContainsKey("password") &&
												e.Params.ContainsKey("password_agree"))
											{
												var resp = Dispatcher.ReadTemplate(ref fs, "content-box");
												resp = resp.Replace("[[content]]", "registeruser");
												resp = resp.Replace("[[box-title]]", "Benutzer erstellen");

												if (users.HandleRegisterRequest(e.Params["username"], e.Params["password"], e.Params["password_agree"]))
													resp = resp.Replace("[[box-content]]", "Du hast dich erfolgreich registriert!");
												else
													resp = resp.Replace("[[box-content]]", "<p class=\"exclaim\">Es trat ein Fehler bei der Registrierung auf.<br>Überprüfe bitte die Eingaben!</p>");

												e.Context.Response.StatusCode = 200;
												e.Context.Response.StatusDescription = "OK";

												e.Context.Response.ContentType = "text/html";
												ws.Send(ref resp, ref e.Context, Encoding.UTF8);

												return;
											}
											break;
										case WebServer.HTTPMethod.GET:
											break;
									}
									break;
								case WebServer.SiteAction.Edit:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var resp = users.GetProfile(ref fs, userid);

										e.Context.Response.StatusCode = 200;
										e.Context.Response.StatusDescription = "OK";

										e.Context.Response.ContentType = "text/html";
										ws.Send(ref resp, ref e.Context, Encoding.UTF8);
									}

									break;
								case WebServer.SiteAction.Remove:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var resp = string.Empty;
										if (e.Params.ContainsKey("uid") && users.CanDeleteUsers(userid))
										{
											var uid = (T)Convert.ChangeType(e.Params["uid"], typeof(T));
											if (users.DeleteUser(uid, userid))
											{
												e.Context.Response.StatusCode = 200;
												e.Context.Response.StatusDescription = "OK";
												resp = users.GetUserList(userid, ref fs);
											}
											else
											{
												e.Context.Response.StatusCode = 404;
												e.Context.Response.StatusDescription = "Der Benutzer wurde nicht gefunden!";
											}
										}
										else
										{
											e.Context.Response.StatusCode = 403;
											e.Context.Response.StatusDescription = "Keine Berechtigung!";
										}

										e.Context.Response.ContentType = "text/html";
										ws.Send(ref resp, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										e.Context.Response.ContentType = "text/html";
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Profile:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var uid = string.IsNullOrEmpty(e.Params["uid"]) ? "0" : e.Params["uid"];
										var edit_resp = string.Empty;

										var profile_id = (T)Convert.ChangeType(uint.Parse(uid), typeof(T));
										if (users.CanEditUsers(userid) || string.Format("{0}",profile_id) == string.Format("{0}", userid))
										{
											edit_resp = users.GetProfile(ref fs, profile_id);
											if (!string.IsNullOrEmpty(edit_resp))
											{
												e.Context.Response.StatusCode = 200;
												e.Context.Response.StatusDescription = "OK";
											}
											else
											{
												e.Context.Response.StatusCode = 404;
												e.Context.Response.StatusDescription = "Der Benutzer wurde nicht gefunden!";
											}
										}
										else
										{
											e.Context.Response.StatusCode = 403;
											e.Context.Response.StatusDescription = "Keine Berechtigung!";
										}

										e.Context.Response.ContentType = "text/html";
										ws.Send(ref edit_resp, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										e.Context.Response.ContentType = "text/html";
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Logout:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var cookie = new Cookie("User", "0", "/");
										cookie.Expired = true;
										cookie.Expires = DateTime.Now.AddDays(-1);

										e.Context.Response.SetCookie(cookie);
										e.Context.Response.StatusCode = 200;
										e.Context.Response.StatusDescription = "OK";
										e.Context.Response.ContentType = "text/html";

										var login_resp = users.GetLoginForm(ref fs);
										ws.Send(ref login_resp, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										e.Context.Response.ContentType = "text/html";
										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Login:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										if (e.Params.ContainsKey("username") && e.Params.ContainsKey("password"))
										{
											var login_user = (T)Convert.ChangeType(users.HandleLoginRequest(
												e.Params["username"], e.Params["password"]), typeof(T));
											var login_resp = string.Empty;

											if (users.UserExists(login_user))
											{
												var cookie = new Cookie("User", string.Format("{0}", login_user), "/");
												cookie.Expires = DateTime.Now.AddDays(1);

												e.Context.Response.SetCookie(cookie);
												login_resp = users.GetUserInfo(login_user, ref fs);
											}
											else
												login_resp = users.GetLoginForm(ref fs);

											e.Context.Response.StatusCode = 200;
											e.Context.Response.StatusDescription = "OK";
											e.Context.Response.ContentType = "text/html";

											ws.Send(ref login_resp, ref e.Context, Encoding.UTF8);
										}
										else
										{
											var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
											e.Context.Response.ContentType = "text/html";

											ws.Send(ref err, ref e.Context);
										}
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										e.Context.Response.ContentType = "text/html";

										ws.Send(ref err, ref e.Context);
									}
									break;
								case WebServer.SiteAction.Show:
									if (e.Context.Request.Headers["UAgent"] == "Namiono")
									{
										var resp_userlist = string.Empty;
										if (users.UserExists(userid))
										{
											resp_userlist = users.GetUserList(userid, ref fs);
											e.Context.Response.StatusCode = 200;
											e.Context.Response.StatusDescription = "OK";
										}
										else
										{
											e.Context.Response.StatusCode = 403;
											e.Context.Response.StatusDescription = "Nicht ausreichende Rechte! (Sind sie (noch) eingeloggt?)";
											resp_userlist = string.Empty;
										}

										e.Context.Response.ContentType = "text/html";
										ws.Send(ref resp_userlist, ref e.Context, Encoding.UTF8);
									}
									else
									{
										var err = ws.SendErrorDocument(this.title, 403, "Request-Headers not valid!", ref e.Context);
										e.Context.Response.ContentType = "text/html";

										ws.Send(ref err, ref e.Context);
									}
									break;
							}
							break;
						case WebServer.Provider.None:
							break;
					}
				}
				else
				{
					if (e.Context.Request.UserAgent == "Mozilla/3.0 (compatible)" && e.Path == "/admin.cgi")
					{
						var t = e.Params.ContainsKey("sid") ? int.Parse(e.Params["sid"]) : 1;
						var data = new byte[0];

						var sc = (from s in ShoutcastServers.Values where s.IsStateServer select s).FirstOrDefault();
						if (sc != null)
						{
							data = Encoding.UTF8.GetBytes(sc.Members[(T)Convert.ChangeType(t, typeof(T))].XML.OuterXml);
							e.Context.Response.StatusCode = 200;
							e.Context.Response.StatusDescription = "OK";
						}
						else
						{
							e.Context.Response.StatusCode = 500;
							e.Context.Response.StatusDescription = "Keine Statistiken verfügbar!";
						}

						e.Context.Response.ContentType = "text/xml";
						ws.Send(ref data, ref e.Context);
					}

					var site = (e.RequestType == WebServer.RequestType.sync) ?
						GenerateSite(e.Path, ref e.Context, p, userid) : Build_content_Boxes(p, userid);

					e.Context.Response.StatusCode = 200;
					e.Context.Response.StatusDescription = "OK";

					ws.Send(ref site, ref e.Context, Encoding.UTF8);
				}
			};
		}

		void LoadSendePlan()
		{
			var sp_sendeplaene = db.SQLQuery<uint>("SELECT spident FROM sendeplan_ident");
			if (sp_sendeplaene.Count != 0)
			{
				lock (SendePlaene)
				{
					for (var i = uint.MinValue; i < sp_sendeplaene.Count; i++)
					{
						if (string.IsNullOrEmpty(sp_sendeplaene[i]["spident"]))
							continue;

						var guid = Guid.Parse(sp_sendeplaene[i]["spident"]);
						if (!SendePlaene.ContainsKey(guid))
						{
							var settings = new Sendeplan<T>.Sp_settings<T>();
							SendePlaene.Add(guid, new Sendeplan<T>(guid, string.Format("Sendeplan {0}", i), ref db, ref fs, ref users,
							  ref settings));
						}
					}
				}
			}
		}

		void LoadShoutcastServers()
		{
			var sc_servers = db.SQLQuery<uint>("SELECT * FROM shoutcast");
			if (sc_servers.Count != 0)
			{
				lock (ShoutcastServers)
				{
					for (var i = uint.MinValue; i < sc_servers.Count; i++)
						if (!ShoutcastServers.ContainsKey(sc_servers[i]["id"]))
						{
							var sc = new ShoutcastServer<T>(ref db, ref fs, ref users, sc_servers[i]["id"], sc_servers[i]["hostname"],
								ushort.Parse(sc_servers[i]["port"]), sc_servers[i]["password"], sc_servers[i]["stateserver"] != "0", sc_servers[i]["servertype"] == "2");

							sc.ShoutcastStarted += (sender, e) => Console.WriteLine(e.Message);

							ShoutcastServers.Add(sc_servers[i]["id"], sc);
						}
				}
			}
		}

		string Build_navigation(T userlevel, T userid)
		{
			var tpl = Dispatcher.ReadTemplate(ref fs, "content-box");
			var navbox = "<ul>";

			var navigation = db.SQLQuery<uint>(string.Format("SELECT * FROM navigation WHERE level <= '{0}' AND target ='navigation'", userlevel));
			if (navigation.Count != 0)
			{
				for (var i = uint.MinValue; i < navigation.Count; i++)
				{
					var url = navigation[i]["link"].Contains(".") ? navigation[i]["link"] : string.Format("/providers/{0}/", navigation[i]["link"]);
					var data = navigation[i]["data"].Replace("[#UID#]", string.Format("{0}", userid));

					navbox += string.Format("<li><a href=\"#\" onclick=\"LoadDocument('{0}', '#content', '{1}', '{2}', '{3}', '')\">{1}</a></li>\n",
						url, navigation[i]["name"], navigation[i]["action"], data);
				}
			}

			navbox += "</ul>";
			return tpl.Replace("[[box-title]]", "Navigation").Replace("[[box-content]]", navbox).Replace("[[content]]", "navigation");
		}

		string Build_content_Boxes(string site, T userid)
		{
			var output = string.Empty;
			var content_boxes = db.SQLQuery<uint>(string.Format("SELECT * FROM content WHERE position='content' AND site='{0}'", site));

			if (content_boxes.Count != 0)
			{
				for (var i = uint.MinValue; i < content_boxes.Count; i++)
				{
					var tpl = Dispatcher.ReadTemplate(ref fs, "content-box");
					tpl = tpl.Replace("[[box-title]]", content_boxes[i]["title"]);
					tpl = tpl.Replace("[[box-content]]", content_boxes[i]["payload"]);
					tpl = tpl.Replace("[[position]]", content_boxes[i]["position"]);
					tpl = tpl.Replace("[[content]]", content_boxes[i]["tag"]);

					output += tpl;
				}
			}

			return output;
		}

		string Build_side_boxes(string position, ref HttpListenerContext context, T userid)
		{
			var output = string.Empty;
			if (position == "aside")
			{
				if (context.Request.Cookies["User"] == null)
				{
					output = users.GetLoginForm(ref fs);
				}
				else
				{
					var cook = context.Request.Cookies["User"].Value;
					if (string.IsNullOrEmpty(cook))
						cook = "0";

					output = (cook != "0") ? users.GetUserInfo((T)Convert.ChangeType(ulong.Parse(cook), typeof(T)), ref fs) : users.GetLoginForm(ref fs);
				}
			}

			var boxes = db.SQLQuery<uint>(string.Format("SELECT * FROM content WHERE position='{0}'", position));
			if (boxes.Count != 0)
			{
				for (var i = byte.MinValue; i < boxes.Count; i++)
				{
					var tpl = Dispatcher.ReadTemplate(ref fs, "content-box");
					if (string.IsNullOrEmpty(boxes[i]["tag"]))
						continue;

					if (boxes[i]["tag"] == "shoutcast" && ShoutcastServers.Count == 0)
						continue;

					tpl = tpl.Replace("[[box-title]]", boxes[i]["title"])
						.Replace("[[box-content]]", boxes[i]["payload"]).Replace("[[content]]", boxes[i]["tag"]);

					if (boxes[i]["tag"] == "shoutcast")
						tpl = tpl.Replace("[[content-shoutcast]]", GenerateShoutcastOutput(userid));

					output += tpl;

					output = output.Replace("[[position]]", position);
				}
			}

			return output;
		}

		string GenerateShoutcastOutput(T userid)
		{
			var sc_output = string.Empty;

			if (ShoutcastServers.Count != 0)
			{
				foreach (var sc in ShoutcastServers.Values)
					sc_output += sc.OutPut(userid);
			}
			else
				sc_output = "<p class=\"exclaim\">Keine Server gefunden!</p>";

			return sc_output;
		}

		string GenerateSite(string path, ref HttpListenerContext context, string page, T userid, bool readHeader = true)
		{
			var site = Dispatcher.ReadTemplate(ref fs, "site-content");

			var grouplevel = (T)Convert.ChangeType(users.GetGroupLevelByID(
				(T)Convert.ChangeType(users.GetUserLevelbyID(userid), typeof(T))), typeof(T));

			var navigation = string.Format("{0}\n{1}", Build_navigation(grouplevel, userid),
				Build_side_boxes("nav", ref context, userid));

			if (!readHeader)
				site = site.Replace("[[site-header]]", string.Empty);

			site = site.Replace("[[content-nav]]", navigation);
			site = site.Replace("[[content-aside]]", Build_side_boxes("aside", ref context, userid));
			site = site.Replace("[[content-content]]", Build_content_Boxes(page, userid));

			while (site.Contains("[[") && site.Contains("]]"))
			{
				var tplname = Dispatcher.GetTPLName(site);
				var content = Dispatcher.ReadTemplate(ref fs, tplname);
				site = site.Replace(string.Format("[[{0}]]", tplname), content);
			}

			site = site.Replace("[#YEAR#]", string.Format("{0}", DateTime.Now.Year));
			site = site.Replace("[#MONTH#]", string.Format("{0}", DateTime.Now.Month));

			site = site.Replace("[#DESIGN#]", users.UserExists(userid) ?
				users.GetUserDesignbyID(userid, this.design) : this.design);

			site = site.Replace("[#SITE_TITLE#]", this.title);
			site = site.Replace("[#APPNAME#]", "Namiono");
#if DEBUG
			site = site.Replace("[#DEBUG_WARN#]", "<p class=\"developer\">Debug-Version! Die Version befindet sich in Entwicklung. Fehler sind zu erwarten.</p>");
#else
			site = site.Replace("[#DEBUG_WARN#]", string.Empty);
#endif
			context.Response.ContentType = "text/html";

			return site;
		}

		public void Start()
		{
			if (realm == Realm.Public)
			{
				Task.Run(() =>
				{
					foreach (var sp in SendePlaene.Values)
						sp?.Start();
				});

				Task.Run(() =>
				{
					foreach (var sc in ShoutcastServers.Values)
						sc?.Start();
				});
			}

			users.Start();
			ws.Start();
		}

		public void HeartBeat()
		{
			if (realm == Realm.Public)
			{
				foreach (var sc in ShoutcastServers.Values)
					sc?.Heartbeat();

				foreach (var sp in SendePlaene.Values)
					sp?.Heartbeat();
			}

			users.HeartBeat();
			db.HeartBeat();
		}

		public void Close()
		{
			if (realm == Realm.Public)
			{
				foreach (var sc in ShoutcastServers.Values)
					sc?.Close();

				foreach (var sp in SendePlaene.Values)
					sp?.Close();
			}

			users?.Close();
			ws?.Close();
		}

		public void Dispose()
		{
			if (realm == Realm.Public)
			{
				foreach (var sc in ShoutcastServers.Values)
					sc?.Dispose();

				foreach (var sp in SendePlaene.Values)
					sp?.Dispose();

				ShoutcastServers.Clear();
				SendePlaene.Clear();
			}

			users.Dispose();
			ws.Dispose();
		}
	}
}
