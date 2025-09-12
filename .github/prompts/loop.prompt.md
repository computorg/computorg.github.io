---
mode: 'agent'
---
the baseline is the screenshot attached to this prompt.

First you have to check this baseline, which will be used in the following loop.
You have to loop over those following steps, don’t stop between steps, don’t stop at the end of the loop, start over :
1. Launch the `quarto render` command to render the site locally
2. Check for any problem in the render command output and fix them
3. Check the the remdered output at http://127.0.0.1:3000/_site/site/publications.html with a screeshot of playwright tool. Compare this screenshot to the baseline. Analyze the differences.
4. use this analysis to modify the template code site/publications.ejs in order to make up for any remaining differences with the baseline or visual issues. Check both doc for quarto and ejs.
5. commit the modification with a meanningful commit message
6. go to 1.