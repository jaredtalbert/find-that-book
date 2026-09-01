
[//]: # (using this to think "out loud" because I won't be able to remember my thoughts otherwise)

- only asks for OpenLibrary but future integrations may want to be considered 
  - use interface; can swap impl later as needed
- letting codex do the boilerplate httpclient setup
- conveniently, the OL search API handles the majority of the fuzzy searching for us
- "fetch additional work or author details when useful" how do we determine usefulness?
- we probably want the server to be in charge of the Gemini client to prevent abuse
  - given more time, we'd want to enforce rate limits