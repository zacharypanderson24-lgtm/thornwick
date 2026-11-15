t=open('template.html').read(); u=open('ui.js').read(); x=open('textures.js').read()
sim=open('sim.js').read().replace("if (typeof module !== 'undefined') module.exports","if (false) module.exports").replace("'use strict';\n","")
open('index.html','w').write(t.replace('/*SIM*/',sim).replace('/*TEX*/',x).replace('/*UI*/',u))
