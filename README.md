# MEH #
*An __m__inimalist, __e__mbeddable, self-__h__osted and self-aware&#42; HTTP server for C#*

### Usage ###

Writing a MEH server is incredibly easy:

		using MEH;

		namespace SuperTrouperApp {
			class Stage : HttpServer {
				// ...
			}
		}

New request handlers (ie. routes in "richer" libraries) are added through `GET` and `POST` methods:

		public Stage(int port) : base(port) {
			POST(@"/feeling/(\w)$", (m, p) => {
				string number = p.inputStream.ReadToEnd();
				p.Respond(String.Format("Feeling like a {0}, {1}", m[1].Value, number));
			});
			GET(@"/", (m, p) => p.Respond("BEAMS ARE GONNA BLIND ME"));
		}

The `m` argument is a [`System.Text.RegularExpressions.GroupCollection`](http://goo.gl/vl5AD), while `p` represents an internal class, `HttpProcessor`. The only thing about `HttpProcessor` that you need to know is its `Respond` method:

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

Oh yes, MEH can serve static files as well. Take a look at the `ServeFiles` method, I don't feel like writing about it right now. meh.

### License ###
MEH is based on the works of [David Jeske](http://www.codeproject.com/Articles/137979/Simple-HTTP-Server-in-C), and as the original work, meh is published to public domain. Have fun!

### Footnotes ###
\* &ndash; self-aware of its limitations