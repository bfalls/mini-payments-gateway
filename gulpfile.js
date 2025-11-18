// gulpfile.js - EC2 control for mini-payments-gateway via AWS CLI + tags

const gulp = require("gulp");
const { execSync } = require("child_process");

// ---- CONFIG: edit here if you ever rename the instance/tag/region ----

// EC2 region for this project
const REGION = "us-east-1";

// Tag used to locate the instance (no instance ID checked in)
const TAG_KEY = "Project";
const TAG_VALUE = "mini-payments-gateway";

// ----------------------------------------------------------------------

function runAws(command, options = {}) {
    const full = `aws ${command}`;
    try {
        const execOptions = {
            stdio: options.stdio || "pipe",
        };
        if (execOptions.stdio !== "inherit") {
            // Only set encoding when we expect output back
            execOptions.encoding = "utf8";
        }

        const result = execSync(full, execOptions);

        // When stdio === 'inherit', result is null; only write if we actually have a string.
        if (!options.silent && typeof result === "string" && result.length > 0) {
            process.stdout.write(result);
        }

        return typeof result === "string" ? result.trim() : "";
    } catch (err) {
        console.error(`AWS CLI command failed:\n  ${full}`);
        if (err.stdout) process.stdout.write(err.stdout.toString());
        if (err.stderr) process.stderr.write(err.stderr.toString());
        throw err;
    }
}

function getInstanceId() {
    const cmd = [
        "ec2 describe-instances",
        `--region ${REGION}`,
        `--filters "Name=tag:${TAG_KEY},Values=${TAG_VALUE}"`,
        '          "Name=instance-state-name,Values=pending,running,stopping,stopped"',
        '--query "Reservations[0].Instances[0].InstanceId"',
        "--output text",
    ].join(" ");

    const id = runAws(cmd, { silent: true });

    if (!id || id === "None") {
        throw new Error(
            `No EC2 instance found with tag ${TAG_KEY}=${TAG_VALUE} in ${REGION}.`
        );
    }

    console.log(`Using instance: ${id}`);
    return id;
}

// ---------------- TASKS ----------------

function ec2Status(done) {
    const id = getInstanceId();
    const cmd = [
        "ec2 describe-instances",
        `--region ${REGION}`,
        `--instance-ids ${id}`,
        '--query "Reservations[0].Instances[0].State.Name"',
        "--output text",
    ].join(" ");

    const state = runAws(cmd, { silent: true });
    console.log(`Instance state: ${state}`);
    done();
}

function ec2Start(done) {
    const id = getInstanceId();
    const cmd = [
        "ec2 start-instances",
        `--region ${REGION}`,
        `--instance-ids ${id}`,
        "--output text",
    ].join(" ");

    runAws(cmd, { stdio: "inherit" });
    done();
}

function ec2Stop(done) {
    const id = getInstanceId();
    const cmd = [
        "ec2 stop-instances",
        `--region ${REGION}`,
        `--instance-ids ${id}`,
        "--output text",
    ].join(" ");

    runAws(cmd, { stdio: "inherit" });
    done();
}

// “Config” = show ID + state + public DNS/IP in one shot
function ec2Config(done) {
    const id = getInstanceId();
    const cmd = [
        "ec2 describe-instances",
        `--region ${REGION}`,
        `--instance-ids ${id}`,
        '--query "Reservations[0].Instances[0].{',
        "InstanceId:InstanceId,",
        "State:State.Name,",
        "PublicIp:PublicIpAddress,",
        "PublicDns:PublicDnsName",
        '}"',
        "--output json",
    ].join(" ");

    const info = runAws(cmd, { silent: true });
    console.log("Instance config:");
    console.log(info);
    done();
}

// Register tasks with gulp (names are what Task Runner Explorer will show)
gulp.task("ec2:status", ec2Status);
gulp.task("ec2:start", ec2Start);
gulp.task("ec2:stop", ec2Stop);
gulp.task("ec2:config", ec2Config);

// Optional: set a default task if you want (e.g., status)
// gulp.task("default", ec2Status);
