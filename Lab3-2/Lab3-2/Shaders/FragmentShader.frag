#version 330 core
out vec4 FragColor;

uniform vec3 uLightColor;         // light source color (diffuse and specular terms)
uniform vec3 uAmbientLightColor;  // ambient light color
uniform vec3 uLightPos;
uniform vec3 uViewPos;

// local illumination coefficients per color component (R, G, B)
uniform vec3 uAmbientStrength;
uniform vec3 uDiffuseStrength;
uniform vec3 uSpecularStrength;

uniform float uShininess;

in vec4 outCol;
in vec3 outNormal;
in vec3 outWorldPosition;

void main()
{
    // vec3 * vec3 is a component-wise product in GLSL
    vec3 ambient = uAmbientStrength * uAmbientLightColor;

    vec3 norm = normalize(outNormal);
    vec3 lightDir = normalize(uLightPos - outWorldPosition);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * uLightColor * uDiffuseStrength;

    vec3 viewDir = normalize(uViewPos - outWorldPosition);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), uShininess);
    vec3 specular = spec * uLightColor * uSpecularStrength;

    vec3 result = (ambient + diffuse + specular) * outCol.rgb;

    FragColor = vec4(result, outCol.w);
}