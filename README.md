# Hardwired

Hardwired is a complete overhaul mod for the electrical system in Stationeers, replacing it with a more realistic system with voltage, current, inductance, capacitance, resistance, etc, that follow real-world physics and equations.

The overall goal is to make the electrical system in Stationeers have a similar level of complexity and realism as the atmospherics or other systems, requiring the player to think through their designs and learn real electrical concepts.

⚠️ This mod is still a work-in-progress. Most features have not yet been added, or are incomplete. ⚠️

## Current Features

- Voltage, resistance, and current
- Resistive heating for cables (max power throughput is determined by the cable's temperature, not a magical limit!)
- AC and DC circuits
- Reactive power and impedance
- Transformers
- AC/DC converters (inverter, rectifier, etc)

## Planned Features

- 3-phase AC power
- AC grid forming mechanics (frequency droop, synchroscope, etc)
- More accurate resistive heating
- Expanded power generation
  - Sterling engine and gas fuel generator could generate different AC frequencies depending on RPM, and different voltages that require matching before joining the grid

## AI Disclosure

In today's world of AI slop, especially in mods, I think it's important to disclose to what extent AI was used in this project.
I personally am very against "generative AI" for things like art, or even code (when used to generate whole modules or projects).
However I think the line is less clear when it comes to using AI as an assistive tool, especially for code development.
While I recognize AI is a useful tool for some developers, I don't use it extensively because for me personally Hardwired is a passion project - I *enjoy* the process of programming, and I want to fully understand the code I'm writing.
That being said however, AI is also rapidly changing the software development field, and I have used it a few times on this project, mostly out of curiosity to keep up with changes in AI development.

With that being said, here is how AI was used for Hardwired:

All of Hardwired's code is fully human written.
AI *has not*, and *will not* be used to directly generate the core code for Hardwired.
To the extent that AI was used during development (explained more below), any code generated was taken only as a suggestion.
Code was never copy-pasted directly from an AI, nor were AI "agents" allowed to directly modify files.

AI *may* be used to generate some unit tests, or help search for and diagnose bugs.

AI *was* consulted for research during various phases of development (e.g. to validate my understanding of the physics or math, help track down bugs or performance issues, to suggest alternative algorithms, etc).
When used for such purposes, I always made sure to have some sort of external validation to ensure the AI wasn't hallucinating some explanation.
Usually this took the form of reading existing literature (textbooks, pre-AI articles, etc), or running tests to validate theories.

AI *was* used to "clean up" some of the text discriptions and documentation for various components.
The original text was fully human written, with AI only being used to suggest changes to make the text more concise or flow better.
In such cases, the AI output was *not* copied directly, but instead only used as a suggestion.

All AI output is reviewed by me before being used (I am the owner of this mod, and I take full responsibility for its contents).