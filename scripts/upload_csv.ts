function csvToJson(csvString: string) {
  const rows = csvString.split("\n");
  const headers = rows[0].split(",");
  const jsonData = [];
  for (let i = 1; i < rows.length - 1; i++) {
    const values = rows[i].split(",");
    const obj = {};
    for (let j = 0; j < headers.length; j++) {
      const key = headers[j].trim();
      const value = values[j].trim();
      obj[key] = value;
    }
    jsonData.push(obj);
  }
  return JSON.stringify(jsonData);
}

let csv = await Bun.file(process.argv[2]).text();

const jsonData = csvToJson(csv);

const url =
  "http://0.0.0.0:7700/indexes/shools/documents?primaryKey=emis_number";

var x = fetch(url, {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    Authorization: "Bearer ZnMMn8fURKp8In2v0qfzhhEOn27jO3egzE-20XZ3uqw",
  },
  body: jsonData,
})
  .then((response) => response.json())
  .then((data) => console.log(data))
  .catch((error) => console.error("Error:", error));

console.log(x);
