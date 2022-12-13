# Use Case Scenario #1 - NDVI mapping - Integration Procedures (Microsoft Planetary Computer Hub)

## Set up an account

It is necessary to be a registered user in order to use the Microsoft Planetary Computer resources. You can register at the registration address:

[Registration](https://planetarycomputer.microsoft.com/account/request)

It can take a few working days to be accepted, you will be informed via email.


## Start a server

Once accepted as a user, you can start a server with Jupyter Lab from the **Hub Main Page** at a link like the following (depending on the world region):

https://pccompute.westeurope.cloudapp.azure.com/compute/hub/home

With a web browser, open link and click on the *Start My Server* button. The *Choose your environment* menu appears.

From the menu, choose **CPU - Python** and press *Start*.

After a few seconds **Jupyter Lab** GUI opens. You will see a *README.md* which contains links to many examples.


## Develop and run the application

You can create a new notebook as you normally would in a Jupyter Lab or upload a ready-made notebook for Microsoft Planetary Computer.

To do the latter, you can use the file ![Jupyter Notebook for NDVI on Microsoft Planetary Computer Hub](ndvi_planetary.ipynb)] in this folder. Upload that file using the *Upload Files* button at the top of the *File Browser* panel.

Open the notebook and adjust the AOI and date range if needed. [10%]

Execute the notebook cell by cell. Some might take a few seconds to complete. Make sure there is no error [50%].

After executing the cell **Save the GeoTIFF file** the file *test_ndvi.tiff* should be created and appear in the file browser. [70%]

Download that file for inspection with a suitable tool. [80%]

After executing the cell **Show NDVI result**, you should see a greyscale image, similar to the GeoTIFF, but at a low resolution, like the one below (brighter colours mean higher NDVI values) [90%]:

![NDVI on Microsoft Planetary Computer Hub](ndvi_planetary.png)

The execution result of the last cell is the visible image for reference and comparison [100%].

After completing the test, go back to the **Hub main page** (the same where you started the server, e.g. https://pccompute.westeurope.cloudapp.azure.com/compute/hub/home) and choose *Stop My Server* to release the computing resources. Your changes are preserved and you can restart the server later continuing from where you left.

