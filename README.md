# meh #
*An __m__inimalist, __e__mbeddable, self-hosted and self-aware\* __H__TTP server for C#*

### Usage ###
Simply include the `MEH.cs` file into your project, add a `use MEH;` line and that's it!

Writing a MEH server is just as easy: simply extend the `MEH.HttpServer` class.

		using MEH;

		namespace SuperTrouperApp {
			class Stage : HttpServer {
				// ...
			}
		}

New HTTP handlers are added through `GET` and `POST` methods:

		public Stage(int port) : base(port) {
			POST(@"/feeling/(\w)$", (m, p) => {
				string number = p.inputStream.ReadToEnd();
				p.Respond(String.Format("Feeling like a {0}, {1}", m[1].Value, number));
			});
			GET(@"/", (m, p) => p.Respond("BEAMS ARE GONNA BLIND ME"));
		}

The `m` argument is a [`System.Text.RegularExpressions.GroupCollection`](http://msdn.microsoft.com/en-us/library/system.text.regularexpressions.groupcollection(v=vs.90).aspx), while `p` represents an internal class, `HttpProcessor`. The only thing about `HttpProcessor` that you need to know is its `Respond` method:

		public Stage(int port) : base(port) {
			GET(@"/find/trouper/w/lights", (m, p) => p.Respond("Shining like the sun?"));
			GET(@"/find/trouper", (m, p) => p.Respond(HttpStatusCodes.NotFound));
			GET(@"/find/tea", (m, p) => p.Respond(HttpStatusCodes.ImATeapot, "Are you a teapot?"));
			GET(@"/find/waldo", (m, p) => {
				Dictionary<string, string> headers = new Dictionary<string, string>();
				headers.add("X-Quadrant", "Left");
				headers.add("X-Clues", "None");
				p.respond(HttpStatusCodes.SeeOther, headers, "Hmmm...");
			});
		}

You're now 200 to start using the "library".

### License ###
meh is based on the works of [David Jeske](http://www.codeproject.com/Articles/137979/Simple-HTTP-Server-in-C) and as the original work is published to public domain. Have fun!

### Footnotes ###
\* self-aware of its limitations