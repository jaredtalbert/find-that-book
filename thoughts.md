
[//]: # (using this to think "out loud" because I won't be able to remember my thoughts otherwise)

- only asks for OpenLibrary but future integrations may want to be considered 
  - use interface; can swap impl later as needed
- letting codex do the boilerplate httpclient setup
- conveniently, the OL search API handles the majority of the fuzzy searching for us
- "fetch additional work or author details when useful" how do we determine usefulness?
- we probably want the server to be in charge of the Gemini client to prevent abuse
  - given more time, we'd want to enforce rate limits
- we can use gemini to build a couple more specific queries
- working with codex on getting a plan together for the ranking algo
  - interview-style back and forth really helped to nail down the overall logic
  - the first-pass implementation of the bulk of the ranking pipeline is pretty big; how can we be sure the tests aren't biased?
  - breaking this into smaller chunks, testing specific functionality after each pass, and nailing down the implementation > fully vibing it

# system design
- backend would likely benefit from horizontal scaling w/ k8s or such
- investigate Gemini's recommendations for load balancing (if any)
  - similarly with OpenLibrary - they seem to have fewer API protections. we may need to ensure we don't DoS them