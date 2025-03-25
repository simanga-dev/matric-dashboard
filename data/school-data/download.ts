async function downloadFile(
  url: string,
  outputFilePath: string,
): Promise<void> {
  // Start a network request
  const response = await fetch(url);

  // Check if the response is OK (status 200)
  if (!response.ok) {
    throw new Error(`Failed to fetch ${url}: ${response.statusText}`);
  }

  // Get the file as a Blob
  const blob = await response.blob();

  // Convert Blob to ArrayBuffer
  const buffer = await blob.arrayBuffer();

  // Write the ArrayBuffer to a file using Bun's fs module
  Bun.write(outputFilePath, new Uint8Array(buffer));
}

downloadFile(
  "https://www.education.gov.za/LinkClick.aspx?fileticket=c2vrRwdZJG8%3d&amp;tabid=466&amp;portalid=0&amp;mid=10308&amp;forcedownload=true",
  "test.xlsx",
);
