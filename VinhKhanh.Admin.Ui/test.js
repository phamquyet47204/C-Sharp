const axios = require('axios');

async function testAi() {
  try {
    const res = await axios.post('http://localhost:5000/api/admin/ai/generate', {
      name: "Ốc Oanh",
      description: "Quán ốc nổi tiếng, hải sản tươi sống, không gian rộng rãi."
    });
    console.log("SUCCESS:");
    console.log(JSON.stringify(res.data, null, 2));
  } catch (error) {
    if (error.response) {
      console.log("ERROR Response:", error.response.status, error.response.data);
    } else {
      console.log("ERROR Message:", error.message);
    }
  }
}

testAi();
