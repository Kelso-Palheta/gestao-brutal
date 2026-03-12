const { chromium } = require('playwright');
(async () => {
    try {
        const browser = await chromium.launch({ headless: true });
        const context = await browser.newContext();
        const page = await context.newPage();
        
        page.on('console', msg => console.log('BROWSER_CONSOLE:', msg.text()));
        page.on('pageerror', err => console.log('BROWSER_ERROR:', err.message));
        
        console.log("Navigating to Totem...");
        await page.goto('http://localhost:5241/totem');
        
        console.log("Adding product...");
        await page.click('text=Brutal Estrela');
        await page.click('button:has-text("ADICIONAR")');
        await page.click('button:has-text("Finalizar Pedido")');
        
        console.log("In Checkout. Clicking Aqui...");
        await page.waitForTimeout(1000);
        await page.click('button:has-text("Aqui")');
        
        console.log("Typing number...");
        await page.fill('input[type="tel"]', '11988887777');
        
        console.log("Clicking away...");
        await page.click('h2:has-text("Seu WhatsApp")');
        
        console.log("Waiting a bit...");
        await page.waitForTimeout(2000);
        
        console.log("Done.");
        await browser.close();
    } catch(e) {
        console.log("SCRIPT ERROR:", e);
    }
})();
