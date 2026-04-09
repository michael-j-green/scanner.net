#!/bin/sh
# Hook called by brother-scan-cli after each page is received.
# SCANNER_FILENAME is set per-page (e.g. scan0.jpeg); unset on the final call (all pages done).
# Page files land in the working directory; the C# service finds them after brother-scan-cli exits.
# Nothing to do here.
