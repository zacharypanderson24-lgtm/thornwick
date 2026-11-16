# Builds the single-file game: src/template.html with the simulation parts (listed in sim_parts.txt, concatenated in order), textures.js and ui.js inlined.
t=open('template.html').read(); u=open('ui.js').read(); x=open('textures.js').read()
parts=[l.strip() for l in open('sim_parts.txt') if l.strip()]
sim='\n'.join(open(p).read() for p in parts).replace("if (typeof module !== 'undefined') module.exports","if (false) module.exports").replace("'use strict';\n","")
open('index.html','w').write(t.replace('/*SIM*/',sim).replace('/*TEX*/',x).replace('/*UI*/',u))
