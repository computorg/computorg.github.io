---
layout: page
permalink: /submit/
title: Submit
description: Overview and general guidelines to submit a contribution to Computo
order: 3
nav: true
---

---

## Overview

Submissions  to Computo  require  both  scientific content  (typically
equations,  codes, and  figures)  and  a proof  that  this content  is
reproducible.  This  is achieved  via  the  standard notebook  systems
available for R,  Python, and Julia, coupled with  the binder building
system.

A Computo submission is thus a git(hub) repository typically containing

- the  source of  the notebook  (a markdown  file with yaml metadata) ;
- auxiliary  files, e.g.:  a $$\mathrm{bib}\TeX  $$ file,  some static
figures  in  the `figs/`  subdirectory,  configuration  files for  the
binder  environment to  setup the  machine that  will build  the final
notebook file.

The  compiled  notebook  will  be generated  directly  in  the  github
repository  via  a github  action  and  published,  if the  action  is
successfull, to a gh-page.
A PDF can be then be submitted
to    the   <a    href="https://computo.scholasticahq.com/for-authors"
style="outline: none; border:  none;">Computo submission platform</a>,
by means of the chrome print function.

More details can be found in the following templates, which serve 
as material for starting to write your submission, and as a
documentation for doing so. The [process is described in a
dedicated post]({{ site.baseurl }}/blog/2021/submission-process/).

## Available templates

<div class="publications">

{% bibliography --file templates %}

</div>

## Submit your work

Once your are  happy with your notebook AND that  the github-action is
successful , you may submit your PDF via Scholastica, our platform for
peer-reviewing:

<div id="scholastica-submission-button" style="margin-top: 10px; margin-bottom: 10px;"><a href="https://computo.scholasticahq.com/for-authors" style="outline: none; border: none;"><img style="outline: none; border: none;" src="https://s3.amazonaws.com/docs.scholastica/law-review-submission-button/submit_via_scholastica.png" alt="Submit to Computo"></a></div>

